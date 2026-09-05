using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Online;
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
        private readonly LeaderboardService _service = new();
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

        public WeeklyLeaderboardCoordinator(AccountService account, CloudSyncService cloud)
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            Instance = this;
            GameDataManager.ProfileChanged += OnProfileChanged;
            _registration = OnlineServicesCoordinator.Register(RetryKey, ProcessAsync, CanRun);
            _localRegistration = OnlineServicesCoordinator.Register(LocalRetryKey, FlushLocalQueueAsync,
                () => !_disposed && GameDataManager.IsLoaded && _stagedRuns.Count > 0);
        }

        /// <summary>Снимает неизменяемый контекст до старта; неизвестная серверная неделя остаётся локальной.</summary>
        public WeeklyRunContext CaptureRunContext(LevelProgressKey key)
        {
            if (!GameDataManager.IsLoaded) return null;
            var board = LeaderboardService.ResolveLeaderboardId(key.LocationId, key.PartOfDayId);
            var owner = GameDataManager.OwnerPlayerId;
            var version = string.IsNullOrWhiteSpace(owner) ? null : ReadJournal(owner).Seasons.FirstOrDefault(item =>
                item.Environment == Environment && item.LeaderboardId == board)?.VersionId;
            return new WeeklyRunContext(owner, GameDataManager.ProfileId, GameDataManager.Generation,
                Environment, board, version);
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
                VersionId = context.VersionId, Score = score, Status = WeeklyRunStatus.AwaitingLocalSave
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
            var results = await _service.GetResultsAsync(locationId, partId);
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
            });
            return snapshot;
        }

        private static string Environment => OnlineServicesCoordinator.EnvironmentName;

        private bool CanRun()
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
            var current = await _service.GetPlayerEntryAsync(run.LeaderboardId);
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
            var accepted = await _service.SubmitVersionedScoreAsync(run.LeaderboardId, run.Score, run.VersionId,
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
            current = await _service.GetPlayerEntryAsync(run.LeaderboardId);
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
            UpdateRun(run);
            ApplyConfirmedRewards(run.OwnerPlayerId);
            PublishRun(run);
        }

        /// <summary>Начисляет 50 XP и applied runId одной транзакцией, в том числе после выбора старого cloud.</summary>
        private void ApplyConfirmedRewards(string owner)
        {
            if (GameDataManager.OwnerPlayerId != owner || !CanRun()) return;
            var pendingRewards = ReadJournal(owner).Runs.Where(run =>
                run.Environment == Environment && run.Status == WeeklyRunStatus.ConfirmedImprovement &&
                !GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId)).ToArray();
            if (pendingRewards.Length == 0) return;

            // Игровые изменения и маркеры дедупликации атомарны; события публикуются после commit.
            var levelChanged = false;
            GameDataManager.ExecuteTransaction(CheckpointReason.WeeklyLeaderboardRecordRewarded, () =>
            {
                foreach (var run in pendingRewards)
                {
                    levelChanged |= _experience.GrantExperienceForWeeklyLeaderboardRecord(
                        GameDataManager.PlayerData, notify: false);
                    GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Add(run.RunId);
                }
            }, () =>
            {
                PlayerExperienceService.PublishCommittedLevelChange(levelChanged);
                DebugManager.DiagEconomy($"[WeeklyLeaderboard] confirmed rewards={pendingRewards.Length} xp={50 * pendingRewards.Length}");
                foreach (var run in pendingRewards) PublishRun(run);
            });
        }

        private void PublishRun(WeeklyLeaderboardRun run)
        {
            var isLocalProfile = run.Status == WeeklyRunStatus.LocalOnly &&
                run.ProfileId == GameDataManager.ProfileId;
            if (RunChanged == null || !isLocalProfile && GameDataManager.OwnerPlayerId != run.OwnerPlayerId) return;
            if (run.Status == WeeklyRunStatus.ConfirmedImprovement &&
                !GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId)) return;
            foreach (Action<WeeklyLeaderboardRun> handler in RunChanged.GetInvocationList())
            {
                try { handler(run); }
                catch (Exception exception)
                {
                    DebugManager.DiagStability($"[WeeklyLeaderboard] result subscriber failed ({exception.GetType().Name}).");
                }
            }
        }

        /// <summary>Отбрасывает более старый запрос версии после нового ответа для той же таблицы.</summary>
        private async Task<LeaderboardVersions> ReadSeasonAsync(
            string owner, string profile, long generation, string board)
        {
            var key = owner + ":" + board;
            var request = ++_seasonRequestSequence;
            _seasonRequests[key] = request;
            var season = await _service.GetSeasonAsync(board);
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
            return journal;
        }

        private static void UpdateJournal(string owner, Action<WeeklyLeaderboardJournal> mutation)
        {
            GameDataManager.ExecuteTechnicalTransaction(() =>
            {
                var journal = ReadJournal(owner);
                mutation(journal);
                GameDataManager.SetJournalJson(JournalKey, JsonUtility.ToJson(journal), owner);
            });
        }

        private static void UpdateRun(WeeklyLeaderboardRun run) => UpdateJournal(run.OwnerPlayerId, journal =>
        {
            var index = journal.Runs.FindIndex(item => item.RunId == run.RunId);
            if (index < 0) throw new InvalidOperationException("Queued leaderboard run is missing.");
            journal.Runs[index] = run;
        });

        private static bool IsCurrentProfile(string owner, string profile, long generation) =>
            GameDataManager.OwnerPlayerId == owner && GameDataManager.ProfileId == profile &&
            GameDataManager.Generation == generation;

        private static bool CanSaveRunContext(WeeklyRunContext context) =>
            GameDataManager.IsLoaded && GameDataManager.ProfileId == context.ProfileId &&
            (string.IsNullOrWhiteSpace(context.OwnerPlayerId) ||
                IsCurrentProfile(context.OwnerPlayerId, context.ProfileId, context.Generation));

        private void EnsureCurrentProfile(string owner, string profile, long generation)
        {
            if (!CanRun() || !IsCurrentProfile(owner, profile, generation))
                throw new OperationCanceledException("Leaderboard profile changed during the operation.");
        }

        private void OnProfileChanged() => OnlineServicesCoordinator.RequestRetry(RetryKey);

        public void Dispose()
        {
            _disposed = true;
            GameDataManager.ProfileChanged -= OnProfileChanged;
            _registration.Dispose();
            _localRegistration.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
