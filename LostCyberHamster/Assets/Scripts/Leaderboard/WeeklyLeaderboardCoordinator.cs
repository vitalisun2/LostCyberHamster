using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Online;
using Assets.Scripts.Tutorial;
using GameManagement.CloudSave;
using GameManagement.Progress;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement.Leaderboard
{
    /// <summary>Сохраняет FIFO забегов, отправляет их в исходную неделю и применяет подтверждённый XP.</summary>
    public sealed class WeeklyLeaderboardCoordinator : IDisposable
    {
        public const string RetryKey = "weekly";
        private const string JournalKey = "weekly";
        private const string LocalRetryKey = "weekly-local-save";
        private readonly AccountService _account;
        private readonly CloudSyncService _cloud;
        private readonly GameNetworkFacade _network;
        private readonly PlayerExperienceService _experience = new();
        private readonly Dictionary<string, long> _seasonRequests = new();
        private readonly Dictionary<string, long> _resultsRequests = new();
        private readonly List<WeeklyLeaderboardRun> _stagedRuns = new();
        private readonly Dictionary<string, WeeklyRunContext> _stagedContexts = new();
        private readonly IDisposable _registration;
        private readonly IDisposable _localRegistration;
        private long _seasonRequestSequence;
        private bool _disposed;

        public static WeeklyLeaderboardCoordinator Instance { get; private set; }
        public event Action<WeeklyLeaderboardRun> RunChanged;
        public event Action RecordsChanged;

        public WeeklyLeaderboardCoordinator(AccountService account, CloudSyncService cloud,
            GameNetworkFacade network)
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            Instance = this;
            GameDataManager.ProfileChanged += OnProfileChanged;
            _registration = OnlineServicesCoordinator.Register(RetryKey, ProcessAsync, CanRun);
            _localRegistration = OnlineServicesCoordinator.Register(LocalRetryKey, FlushLocalQueueAsync,
                () => !_disposed && GameDataManager.IsLoaded && !TutorialStorage.IsPlayerDataBackupActive);
        }

        /// <summary>Возобновляет локальные сохранения и выплаты после восстановления основного профиля.</summary>
        public void RetryPendingRewards() => OnlineServicesCoordinator.RequestRetry(LocalRetryKey);

        /// <summary>Снимает неизменяемый контекст до старта; неизвестная серверная неделя остаётся локальной.</summary>
        public WeeklyRunContext CaptureRunContext(LevelProgressKey key)
        {
            if (!GameDataManager.IsLoaded) return null;
            var board = LeaderboardService.ResolveLeaderboardId(key.LocationId, key.PartOfDayId);
            var owner = GameDataManager.OwnerPlayerId;
            var journal = ReadJournal(owner);
            var season = string.IsNullOrWhiteSpace(owner) ? null : journal.Seasons.FirstOrDefault(item =>
                item.Environment == Environment && item.LeaderboardId == board);
            var baseline = IsCurrentSeason(season) ? journal.PersonalBests.FirstOrDefault(item =>
                item.OwnerPlayerId == owner && item.ProfileId == GameDataManager.ProfileId &&
                item.Environment == Environment && item.LeaderboardId == board &&
                item.VersionId == season.VersionId) : null;
            return new WeeklyRunContext(owner, GameDataManager.ProfileId, GameDataManager.Generation,
                Environment, board, season?.VersionId, baseline);
        }

        /// <summary>Возвращает непросмотренные подтверждения актуальной недели текущего профиля.</summary>
        public IReadOnlyList<WeeklyRecordNotification> GetPendingRecordNotifications()
        {
            if (_disposed || !GameDataManager.IsLoaded || string.IsNullOrWhiteSpace(GameDataManager.OwnerPlayerId) ||
                _cloud.HasUnresolvedConflict || AccountTransitionScope.IsActive ||
                GameDataManager.IsProfileReplacementBlocked || TutorialStorage.IsPlayerDataBackupActive)
                return Array.Empty<WeeklyRecordNotification>();
            var owner = GameDataManager.OwnerPlayerId;
            var journal = ReadJournal(owner);

            // Историческая база инициализируется при следующей записи журнала до нового подтверждения.
            if (journal.RecordPresentationVersion == 0) return Array.Empty<WeeklyRecordNotification>();
            return journal.Runs.Where(run => run.OwnerPlayerId == owner &&
                    run.ProfileId == GameDataManager.ProfileId && run.Environment == Environment &&
                    run.Status == WeeklyRunStatus.ConfirmedImprovement &&
                    GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId) &&
                    !journal.AcknowledgedRecordRunIds.Contains(WeeklyRecordNotification.GetNotificationId(run)) &&
                    journal.Seasons.Any(season => season.Environment == run.Environment &&
                        season.LeaderboardId == run.LeaderboardId && season.VersionId == run.VersionId &&
                        IsCurrentSeason(season)))
                .Select(run => new WeeklyRecordNotification(run)).ToArray();
        }

        /// <summary>Сохраняет факт читаемого показа toast или подтверждения в Win; выплату не меняет.</summary>
        public bool AcknowledgeRecordNotification(WeeklyRecordNotification notification)
        {
            if (notification == null || !GetPendingRecordNotifications().Any(item =>
                    item.NotificationId == notification.NotificationId)) return false;
            UpdateJournal(notification.OwnerPlayerId, journal =>
            {
                if (!journal.AcknowledgedRecordRunIds.Contains(notification.NotificationId))
                    journal.AcknowledgedRecordRunIds.Add(notification.NotificationId);
            });
            PublishRecordsChanged();
            return true;
        }

        /// <summary>Ставит каждый забег в FIFO; при сбое диска повторяет сохранение с тем же runId.</summary>
        public WeeklyLeaderboardRun QueueSuccessfulRun(WeeklyRunContext context, int score)
        {
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            if (context == null || context.Environment != Environment || !CanSaveRunContext(context))
                return null;

            // Техническая очередь входит в тот же envelope, что и игровой прогресс.
            var run = new WeeklyLeaderboardRun
            {
                RunId = Guid.NewGuid().ToString("N"), OwnerPlayerId = context.OwnerPlayerId,
                ProfileId = context.ProfileId,
                Environment = context.Environment, LeaderboardId = context.LeaderboardId,
                VersionId = context.VersionId, Score = score, Status = WeeklyRunStatus.AwaitingLocalSave,
                LocalOnlyReason = string.IsNullOrWhiteSpace(context.OwnerPlayerId)
                    ? WeeklyLocalOnlyReason.OwnerUnassigned
                    : string.IsNullOrWhiteSpace(context.VersionId)
                        ? WeeklyLocalOnlyReason.SeasonUnknown
                        : WeeklyLocalOnlyReason.None
            };
            _stagedRuns.Add(run);
            _stagedContexts.Add(run.RunId, context);
            try { FlushLocalQueue(); }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[WeeklyLeaderboard] local queue save pending ({exception.GetType().Name}).");
                OnlineServicesCoordinator.RequestRetry(LocalRetryKey);
            }
            return run;
        }

        private Task FlushLocalQueueAsync()
        {
            FlushLocalQueue();
            ApplyConfirmedRewards(GameDataManager.OwnerPlayerId);
            return Task.CompletedTask;
        }

        /// <summary>Переносит volatile intent в общий envelope до сетевой отправки.</summary>
        private void FlushLocalQueue()
        {
            while (_stagedRuns.Count > 0)
            {
                var run = _stagedRuns[0];
                var context = _stagedContexts[run.RunId];
                if (!CanSaveRunContext(context))
                {
                    _stagedRuns.RemoveAt(0);
                    _stagedContexts.Remove(run.RunId);
                    continue;
                }

                // Тот же runId защищает повтор локальной записи; ошибка сохраняет volatile intent.
                // Неизвестная до старта неделя навсегда остаётся локальной, даже после первого входа.
                run.Status = string.IsNullOrWhiteSpace(run.OwnerPlayerId) || string.IsNullOrWhiteSpace(run.VersionId)
                    ? WeeklyRunStatus.LocalOnly : WeeklyRunStatus.Pending;
                try
                {
                    UpdateJournal(run.OwnerPlayerId, journal =>
                    {
                        if (!journal.Runs.Any(item => item.RunId == run.RunId)) journal.Runs.Add(run);
                    });
                }
                catch
                {
                    run.Status = WeeklyRunStatus.AwaitingLocalSave;
                    throw;
                }
                _stagedRuns.RemoveAt(0);
                _stagedContexts.Remove(run.RunId);
                PublishRun(run);
                if (run.Status == WeeklyRunStatus.Pending) OnlineServicesCoordinator.RequestRetry(RetryKey);
            }
        }

        /// <summary>Возвращает кеш текущего владельца и среды, отмечая прошлую серверную неделю.</summary>
        public bool TryGetCachedResults(string locationId, string partId, out LeaderboardResultsSnapshot snapshot)
        {
            snapshot = null;
            if (!GameDataManager.IsLoaded || string.IsNullOrWhiteSpace(GameDataManager.OwnerPlayerId))
                return false;
            var board = LeaderboardService.ResolveLeaderboardId(locationId, partId);
            var owner = GameDataManager.OwnerPlayerId;
            var journal = ReadJournal(owner);
            var version = journal.Seasons.FirstOrDefault(item =>
                item.Environment == Environment && item.LeaderboardId == board)?.VersionId;
            snapshot = journal.CachedResults.FirstOrDefault(item => item.OwnerPlayerId == owner &&
                item.Environment == Environment && item.LeaderboardId == board);
            if (snapshot != null) snapshot.IsPreviousWeek = snapshot.VersionId != version;
            return snapshot != null;
        }

        /// <summary>Возвращает последний забег текущего профиля и таблицы, включая локально сохранённый.</summary>
        public WeeklyLeaderboardRun GetLatestRun(string locationId, string partId)
        {
            if (_disposed || !GameDataManager.IsLoaded) return null;
            var board = LeaderboardService.ResolveLeaderboardId(locationId, partId);
            var owner = GameDataManager.OwnerPlayerId;
            var profile = GameDataManager.ProfileId;

            // Сохраняем видимость LocalOnly исходного профиля после явного принятия владельца.
            return ReadJournal(owner).Runs
                .Concat(_stagedRuns.Where(run => CanSaveRunContext(_stagedContexts[run.RunId])))
                .LastOrDefault(run =>
                    run.Environment == Environment && run.LeaderboardId == board && run.ProfileId == profile &&
                    (run.OwnerPlayerId == owner || string.IsNullOrWhiteSpace(run.OwnerPlayerId) &&
                        (string.IsNullOrWhiteSpace(owner) || run.Status == WeeklyRunStatus.LocalOnly ||
                         run.Status == WeeklyRunStatus.AwaitingLocalSave)));
        }

        /// <summary>Сохраняет проверенный период просмотра для будущих забегов подтверждённого владельца.</summary>
        public bool TryRememberReadSeason(string board, LeaderboardVersions season,
            string owner, string profile, long generation)
        {
            if (season == null || !CanUseCurrentOwner() || !IsCurrentProfile(owner, profile, generation))
                return false;

            // Поздний ответ просмотра сохраняет уже известную более новую серверную неделю.
            var existing = ReadJournal(owner).Seasons.FirstOrDefault(item =>
                item.Environment == Environment && item.LeaderboardId == board);
            if (existing != null && existing.VersionId != season.VersionId &&
                DateTime.TryParse(existing.NextResetUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var knownReset) &&
                knownReset.ToUniversalTime() > season.NextReset.ToUniversalTime())
                return false;

            // Подтверждённое чтение также отменяет запись ответов уже начатых фоновых запросов.
            RememberSeason(owner, board, season);
            _seasonRequests[owner + ":" + board] = ++_seasonRequestSequence;
            return true;
        }

        /// <summary>Загружает рейтинг и проверяет, что все ответы относятся к одной серверной неделе.</summary>
        public async Task<LeaderboardResultsSnapshot> GetResultsAsync(string locationId, string partId)
        {
            if (!CanRun()) throw new InvalidOperationException("Leaderboard connection is unavailable.");
            var owner = GameDataManager.OwnerPlayerId;
            var profile = GameDataManager.ProfileId;
            var generation = GameDataManager.Generation;
            var board = LeaderboardService.ResolveLeaderboardId(locationId, partId);
            var requestKey = owner + ":" + board;
            var request = ++_seasonRequestSequence;
            _resultsRequests[requestKey] = request;

            // Читаем версию с обеих сторон сетевой загрузки, чтобы пережить weekly reset.
            var season = await ReadSeasonAsync(owner, profile, generation, board);
            var results = await _network.GetResultsAsync(locationId, partId);
            EnsureCurrentProfile(owner, profile, generation);
            var after = await ReadSeasonAsync(owner, profile, generation, board);
            var currentVersion = ReadJournal(owner).Seasons.FirstOrDefault(item =>
                item.Environment == Environment && item.LeaderboardId == board)?.VersionId;
            if (_resultsRequests[requestKey] != request ||
                season.VersionId != after.VersionId || currentVersion != after.VersionId)
                throw new OperationCanceledException("Leaderboard week changed while loading.");

            // Время обозначает завершение загрузки на устройстве, а не серверное время.
            var snapshot = new LeaderboardResultsSnapshot
            {
                OwnerPlayerId = owner, Environment = Environment, LeaderboardId = board,
                VersionId = season.VersionId, FetchedAtUtc = DateTime.UtcNow.ToString("o"),
                Entries = results.Top.Select(LeaderboardCachedEntry.FromEntry).ToList(),
                Player = LeaderboardCachedEntry.FromEntry(results.CurrentPlayer)
            };
            UpdateJournal(owner, journal =>
            {
                journal.CachedResults.RemoveAll(item => item.Environment == Environment && item.LeaderboardId == board);
                journal.CachedResults.Add(snapshot);
                RememberPersonalBest(journal, owner, profile, Environment, board, season.VersionId,
                    snapshot.Player != null, snapshot.Player == null ? 0 : checked((int)snapshot.Player.Score));
            });
            return snapshot;
        }

        private static string Environment => OnlineServicesCoordinator.EnvironmentName;

        private bool CanRun() => !_network.IsForcedOffline && CanUseCurrentOwner();

        /// <summary>Проверяет владельца и согласование облака независимо от режима сети.</summary>
        private bool CanUseCurrentOwner()
        {
            try
            {
                return !_disposed && GameDataManager.IsLoaded && OnlineServicesCoordinator.UnityServicesReady &&
                    !_cloud.HasUnresolvedConflict && _account.TryGetAuthenticatedPlayerId(out var playerId) &&
                    playerId == GameDataManager.OwnerPlayerId &&
                    (!_account.TryGetLinkedPlayerId(out _) || _cloud.IsInitialReconciliationComplete);
            }
            catch (InvalidOperationException) { return false; }
        }

        /// <summary>Повторяет FIFO после подключения; сетевые ошибки оставляют запись pending.</summary>
        private async Task ProcessAsync()
        {
            if (!CanRun()) return;
            var owner = GameDataManager.OwnerPlayerId;
            var profile = GameDataManager.ProfileId;
            var generation = GameDataManager.Generation;
            ApplyConfirmedRewards(owner);

            // Каждая запись перечитывается после await: cloud apply может заменить игровой снимок.
            while (CanRun())
            {
                EnsureCurrentProfile(owner, profile, generation);
                var run = ReadJournal(owner).Runs.FirstOrDefault(item =>
                    item.Environment == Environment && item.Status == WeeklyRunStatus.Pending);
                if (run == null) break;
                await ProcessRunAsync(run, profile, generation);
            }

            // Заранее получаем серверную неделю для следующих офлайн-забегов всех локаций.
            foreach (var board in LeaderboardService.ConfiguredLeaderboardIds)
            {
                EnsureCurrentProfile(owner, profile, generation);
                await ReadSeasonAsync(owner, profile, generation, board);
            }
        }

        private async Task ProcessRunAsync(WeeklyLeaderboardRun run, string profile, long generation)
        {
            var season = await ReadSeasonAsync(run.OwnerPlayerId, profile, generation, run.LeaderboardId);
            if (season.VersionId != run.VersionId)
            {
                Complete(run, WeeklyRunStatus.Expired, 0);
                return;
            }

            // Восстанавливаем потерянный ACK по точному runId, score и сохранённой исходной базе.
            var current = await _network.GetPlayerEntryAsync(run.LeaderboardId);
            EnsureCurrentProfile(run.OwnerPlayerId, profile, generation);
            if (run.SendAttempted && ProvesImprovement(run, current))
            {
                Complete(run, WeeklyRunStatus.ConfirmedImprovement, run.Score);
                return;
            }
            if (current != null && current.Score >= run.Score)
            {
                await CompleteComparedRunAsync(run, current, profile, generation);
                return;
            }

            // Сохраняем intent до отправки. Повтор всегда использует прежний runId и previousBest.
            if (!run.SendAttempted)
            {
                run.HadPreviousEntry = current != null;
                run.PreviousBest = current == null ? 0 : checked((int)current.Score);
                run.SendAttempted = true;
                UpdateRun(run);
            }
            var accepted = await _network.SubmitVersionedScoreAsync(run.LeaderboardId, run.Score, run.VersionId,
                new WeeklyScoreMetadata
                {
                    runId = run.RunId, previousBest = run.PreviousBest,
                    hadPreviousEntry = run.HadPreviousEntry
                });

            // Поздний ответ сохраняет receipt исходному owner; чужой активный профиль XP не получает.
            if (ProvesImprovement(run, accepted))
            {
                Complete(run, WeeklyRunStatus.ConfirmedImprovement, run.Score);
                return;
            }
            EnsureCurrentProfile(run.OwnerPlayerId, profile, generation);
            current = await _network.GetPlayerEntryAsync(run.LeaderboardId);
            EnsureCurrentProfile(run.OwnerPlayerId, profile, generation);
            if (ProvesImprovement(run, current))
                Complete(run, WeeklyRunStatus.ConfirmedImprovement, run.Score);
            else if (current != null && current.Score >= run.Score)
                await CompleteComparedRunAsync(run, current, profile, generation);
            else
                throw new InvalidOperationException("Leaderboard acceptance is still pending confirmation.");
        }

        private async Task CompleteComparedRunAsync(WeeklyLeaderboardRun run, LeaderboardEntry current,
            string profile, long generation)
        {
            // Unversioned score мог прийти уже после reset: проверяем неделю до терминального решения.
            var season = await ReadSeasonAsync(run.OwnerPlayerId, profile, generation, run.LeaderboardId);
            if (season.VersionId != run.VersionId)
                Complete(run, WeeklyRunStatus.Expired, 0);
            else
                Complete(run, run.SendAttempted ? WeeklyRunStatus.Unconfirmed : WeeklyRunStatus.NotImproved,
                    checked((int)current.Score));
        }

        private static bool ProvesImprovement(WeeklyLeaderboardRun run, LeaderboardEntry entry)
        {
            if (entry == null || entry.PlayerId != run.OwnerPlayerId || entry.Score != run.Score ||
                string.IsNullOrWhiteSpace(entry.Metadata) || run.HadPreviousEntry && run.Score <= run.PreviousBest)
                return false;
            try
            {
                var metadata = JsonUtility.FromJson<WeeklyScoreMetadata>(entry.Metadata);
                return metadata != null && metadata.schema == 1 && metadata.runId == run.RunId &&
                    metadata.previousBest == run.PreviousBest && metadata.hadPreviousEntry == run.HadPreviousEntry;
            }
            catch (ArgumentException) { return false; }
        }

        private void Complete(WeeklyLeaderboardRun run, WeeklyRunStatus status, int weeklyBest)
        {
            run.Status = status;
            run.WeeklyBest = weeklyBest;
            UpdateJournal(run.OwnerPlayerId, journal =>
            {
                // Квота фиксируется с первым durable-подтверждением, до любых блокировок выплаты.
                if (status == WeeklyRunStatus.ConfirmedImprovement)
                    run.RewardDecision = journal.Runs.FirstOrDefault(item => item.RunId == run.RunId)?.RewardDecision ??
                        CreateRewardDecision(journal, run, DateTime.UtcNow, legacy: false);
                ReplaceRun(journal, run);
                if (status == WeeklyRunStatus.ConfirmedImprovement || status == WeeklyRunStatus.NotImproved ||
                    status == WeeklyRunStatus.Unconfirmed)
                    RememberPersonalBest(journal, run.OwnerPlayerId, run.ProfileId, run.Environment, run.LeaderboardId,
                        run.VersionId, true, weeklyBest);
            });
            OnlineServicesCoordinator.RequestRetry(LocalRetryKey);
            ApplyConfirmedRewards(run.OwnerPlayerId);
            PublishRun(run);
        }

        /// <summary>Применяет сохранённые решения 0/5 и applied runId одной транзакцией после cloud/tutorial.</summary>
        private void ApplyConfirmedRewards(string owner)
        {
            // Durable-подтверждение принадлежит сохранённому владельцу и не требует действующей сессии.
            if (_disposed || !GameDataManager.IsLoaded || string.IsNullOrWhiteSpace(owner) ||
                GameDataManager.OwnerPlayerId != owner || _cloud.HasUnresolvedConflict ||
                AccountTransitionScope.IsActive || GameDataManager.IsProfileReplacementBlocked ||
                TutorialStorage.IsPlayerDataBackupActive) return;
            // Старые оплаченные run сохраняют свою квитанцию; ожидающие впервые получают дату наблюдения.
            if (ReadJournal(owner).Runs.Any(run => run.Status == WeeklyRunStatus.ConfirmedImprovement && run.RewardDecision == null))
                UpdateJournal(owner, journal =>
                {
                    DateTime observedAt = DateTime.UtcNow;
                    foreach (var run in journal.Runs.Where(item => item.Status == WeeklyRunStatus.ConfirmedImprovement && item.RewardDecision == null))
                        run.RewardDecision = CreateRewardDecision(journal, run, observedAt,
                            GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId));
                });
            var pendingRewards = ReadJournal(owner).Runs.Where(run =>
                run.OwnerPlayerId == owner && run.Environment == Environment &&
                run.Status == WeeklyRunStatus.ConfirmedImprovement &&
                !GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId)).ToArray();
            if (pendingRewards.Length == 0) return;

            // Игровые изменения и маркеры дедупликации атомарны; события публикуются после commit.
            var levelChanged = false;
            GameDataManager.ExecuteTransaction(CheckpointReason.WeeklyLeaderboardRecordRewarded, () =>
            {
                foreach (var run in pendingRewards)
                {
                    levelChanged |= _experience.GrantExperienceForWeeklyLeaderboardRecord(
                        GameDataManager.PlayerData, run.RewardDecision.AwardedExperience, notify: false);
                    GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Add(run.RunId);
                }
            }, () =>
            {
                PlayerExperienceService.PublishCommittedLevelChange(levelChanged);
                DebugManager.DiagEconomy($"[WeeklyLeaderboard] confirmed rewards={pendingRewards.Length} xp={pendingRewards.Sum(run => run.RewardDecision.AwardedExperience)}");
                foreach (var run in pendingRewards)
                {
                    FirstSessionTelemetry.Record("record_confirmed", run.RunId, run.Score);
                    PublishRun(run);
                }
            });
        }

        /// <summary>Выбирает одну выплату на owner/environment/UTC-день по всем доскам и неделям.</summary>
        internal static WeeklyDailyRewardDecision CreateRewardDecision(WeeklyLeaderboardJournal journal,
            WeeklyLeaderboardRun run, DateTime confirmedAt, bool legacy)
        {
            var existing = journal.Runs.FirstOrDefault(item => item.RunId == run.RunId)?.RewardDecision;
            if (existing != null) return existing;
            string day = confirmedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            bool dayUsed = journal.Runs.Any(item => item.OwnerPlayerId == run.OwnerPlayerId &&
                item.Environment == run.Environment && item.RunId != run.RunId &&
                item.RewardDecision != null && !item.RewardDecision.IsLegacyReward &&
                item.RewardDecision.UtcDate == day && item.RewardDecision.AwardedExperience > 0);
            return new WeeklyDailyRewardDecision
            {
                RewardId = run.RunId,
                FirstConfirmedAtUtc = legacy ? null : confirmedAt.ToString("o", CultureInfo.InvariantCulture),
                UtcDate = legacy ? null : day,
                AwardedExperience = legacy ? 50 : dayUsed ? 0 : PlayerExperienceService.WeeklyLeaderboardRecordExperienceReward,
                IsLegacyReward = legacy
            };
        }

        private void PublishRun(WeeklyLeaderboardRun run)
        {
            var isLocalProfile = run.Status == WeeklyRunStatus.LocalOnly &&
                run.ProfileId == GameDataManager.ProfileId;
            if (!isLocalProfile && GameDataManager.OwnerPlayerId != run.OwnerPlayerId) return;
            if (run.Status == WeeklyRunStatus.ConfirmedImprovement &&
                !GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId)) return;
            if (run.Status == WeeklyRunStatus.ConfirmedImprovement) PublishRecordsChanged();
            if (RunChanged == null) return;
            foreach (Action<WeeklyLeaderboardRun> handler in RunChanged.GetInvocationList())
            {
                try { handler(run); }
                catch (Exception exception)
                {
                    DebugManager.DiagStability($"[WeeklyLeaderboard] result subscriber failed ({exception.GetType().Name}).");
                }
            }
        }

        private void PublishRecordsChanged()
        {
            if (RecordsChanged == null) return;
            foreach (Action handler in RecordsChanged.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception)
                {
                    DebugManager.DiagStability($"[WeeklyLeaderboard] record subscriber failed ({exception.GetType().Name}).");
                }
            }
        }

        private static bool IsCurrentSeason(LeaderboardSeasonContext season) => season != null &&
            !string.IsNullOrWhiteSpace(season.VersionId) &&
            DateTime.TryParse(season.NextResetUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var reset) && reset.ToUniversalTime() > DateTime.UtcNow;

        /// <summary>Обновляет baseline только внутри точного scope; поздний меньший best его не понижает.</summary>
        private static void RememberPersonalBest(WeeklyLeaderboardJournal journal, string owner, string profile,
            string environment, string board, string version, bool hadEntry, int score)
        {
            var existing = journal.PersonalBests.FirstOrDefault(item => item.OwnerPlayerId == owner &&
                item.ProfileId == profile && item.Environment == environment && item.LeaderboardId == board &&
                item.VersionId == version);
            if (existing != null && existing.HadEntry && (!hadEntry || existing.Score > score)) return;

            // Заменяем baseline недели; уже начатая попытка хранит собственный снимок.
            journal.PersonalBests.RemoveAll(item => item.OwnerPlayerId == owner && item.ProfileId == profile &&
                item.Environment == environment && item.LeaderboardId == board && item.VersionId == version);
            journal.PersonalBests.Add(new WeeklyPersonalBest
            {
                OwnerPlayerId = owner, ProfileId = profile, Environment = environment, LeaderboardId = board,
                VersionId = version, HadEntry = hadEntry, Score = score, FetchedAtUtc = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>Отбрасывает более старый запрос версии после нового ответа для той же таблицы.</summary>
        private async Task<LeaderboardVersions> ReadSeasonAsync(
            string owner, string profile, long generation, string board)
        {
            var key = owner + ":" + board;
            var request = ++_seasonRequestSequence;
            _seasonRequests[key] = request;
            var season = await _network.GetSeasonAsync(board);
            EnsureCurrentProfile(owner, profile, generation);
            if (_seasonRequests[key] == request) RememberSeason(owner, board, season);
            return season;
        }

        private static void RememberSeason(string owner, string board, LeaderboardVersions season)
        {
            if (string.IsNullOrWhiteSpace(season.VersionId))
                throw new InvalidOperationException("Leaderboard server version is missing.");
            var nextReset = season.NextReset.ToUniversalTime().ToString("o");
            var existing = ReadJournal(owner).Seasons.FirstOrDefault(item =>
                item.Environment == Environment && item.LeaderboardId == board);
            if (existing?.VersionId == season.VersionId && existing.NextResetUtc == nextReset) return;

            // Записываем envelope только при изменении полученного серверного контекста.
            UpdateJournal(owner, journal =>
            {
                journal.Seasons.RemoveAll(item => item.Environment == Environment && item.LeaderboardId == board);
                journal.Seasons.Add(new LeaderboardSeasonContext
                {
                    Environment = Environment, LeaderboardId = board, VersionId = season.VersionId,
                    NextResetUtc = nextReset
                });
            });
        }

        private static WeeklyLeaderboardJournal ReadJournal(string owner)
        {
            var json = GameDataManager.GetJournalJson(JournalKey, owner);
            var journal = string.IsNullOrWhiteSpace(json) ? new WeeklyLeaderboardJournal() :
                JsonUtility.FromJson<WeeklyLeaderboardJournal>(json);
            if (journal == null) throw new InvalidOperationException("Weekly leaderboard journal is invalid.");
            journal.Runs ??= new List<WeeklyLeaderboardRun>();
            journal.Seasons ??= new List<LeaderboardSeasonContext>();
            journal.CachedResults ??= new List<LeaderboardResultsSnapshot>();
            journal.PersonalBests ??= new List<WeeklyPersonalBest>();
            journal.AcknowledgedRecordRunIds ??= new List<string>();
            return journal;
        }

        private static void UpdateJournal(string owner, Action<WeeklyLeaderboardJournal> mutation)
        {
            GameDataManager.ExecuteTechnicalTransaction(() =>
            {
                var journal = ReadJournal(owner);
                if (journal.RecordPresentationVersion == 0)
                {
                    journal.RecordPresentationVersion = 1;
                    journal.AcknowledgedRecordRunIds.AddRange(journal.Runs
                        .Where(run => run.Status == WeeklyRunStatus.ConfirmedImprovement)
                        .Select(WeeklyRecordNotification.GetNotificationId));
                }
                mutation(journal);
                var retainedRecordIds = new HashSet<string>(journal.Runs.Select(WeeklyRecordNotification.GetNotificationId));
                journal.AcknowledgedRecordRunIds.RemoveAll(id => !retainedRecordIds.Contains(id));
                GameDataManager.SetJournalJson(JournalKey, JsonUtility.ToJson(journal), owner);
            });
        }

        private static void UpdateRun(WeeklyLeaderboardRun run) => UpdateJournal(run.OwnerPlayerId,
            journal => ReplaceRun(journal, run));

        private static void ReplaceRun(WeeklyLeaderboardJournal journal, WeeklyLeaderboardRun run)
        {
            var index = journal.Runs.FindIndex(item => item.RunId == run.RunId);
            if (index < 0) throw new InvalidOperationException("Queued leaderboard run is missing.");
            journal.Runs[index] = run;
        }

        private static bool IsCurrentProfile(string owner, string profile, long generation) =>
            GameDataManager.OwnerPlayerId == owner && GameDataManager.ProfileId == profile &&
            GameDataManager.Generation == generation;

        private static bool CanSaveRunContext(WeeklyRunContext context) =>
            GameDataManager.IsLoaded && GameDataManager.ProfileId == context.ProfileId &&
            (string.IsNullOrWhiteSpace(context.OwnerPlayerId) ||
                IsCurrentProfile(context.OwnerPlayerId, context.ProfileId, context.Generation));

        private void EnsureCurrentProfile(string owner, string profile, long generation)
        {
            if (!CanUseCurrentOwner() || !IsCurrentProfile(owner, profile, generation))
                throw new OperationCanceledException("Leaderboard profile changed during the operation.");
        }

        private void OnProfileChanged()
        {
            OnlineServicesCoordinator.RequestRetry(RetryKey);
            OnlineServicesCoordinator.RequestRetry(LocalRetryKey);
        }

        public void Dispose()
        {
            _disposed = true;
            GameDataManager.ProfileChanged -= OnProfileChanged;
            _registration.Dispose();
            _localRegistration.Dispose();
            RunChanged = null;
            RecordsChanged = null;
            if (Instance == this) Instance = null;
        }
    }
}
