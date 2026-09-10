#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement;
using GameManagement.Leaderboard;
using GameManagement.Progress;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.DevTools.ExperienceProgressTesting
{
    /// <summary>Тихо завершает непройденные уровни через штатные progress, leaderboard и XP пути.</summary>
    public sealed class ExperienceProgressTestRunner
    {
        private readonly struct PlayerExperienceSnapshot
        {
            public PlayerExperienceSnapshot(
                int playerLevel,
                int experiencePoints)
            {
                PlayerLevel = playerLevel;
                ExperiencePoints = experiencePoints;
                TotalExperience = checked(
                    (playerLevel - 1) *
                    PlayerExperienceService.PlayerLevelThreshold +
                    experiencePoints);
            }

            public int PlayerLevel { get; }

            public int ExperiencePoints { get; }

            public int TotalExperience { get; }
        }

        private const int MaxRandomRunScore = 100;
        private const string ConsoleLogTag = "[XP/Level Progress Testing]";

        private readonly Dictionary<string, (LevelProgress Level, int StarsExperience,
            PlayerExperienceSnapshot BeforeCompletion, string Owner, string Profile, long Generation)> _trackedRuns = new();
        private WeeklyLeaderboardCoordinator _coordinator;
        private int _operationVersion;

        private bool _isBusy;
        private int? _preparedRunScore;
        private string _targetLevelAddress = string.Empty;
        private string _status =
            "Запустите игру через Bootstrap и оставайтесь в Main Menu.";
        private string _firstSessionState = "Нажмите «Обновить состояние первой сессии».";
        private string _inspectedProfile;
        private long _inspectedGeneration;

        private ExperienceProgressTestRunner()
        {
        }

        public static ExperienceProgressTestRunner Shared { get; } = new();

        public event Action Changed;

        public bool IsBusy => _isBusy;

        public bool IsMainMenuReady => Application.isPlaying && IsMainMenuShown();

        public bool CanPrepareNewRecord =>
            !_isBusy &&
            !_preparedRunScore.HasValue &&
            IsMainMenuReady &&
            IsGameDataReady() &&
            TryGetTargetLevel(out _);

        public bool CanCompleteNextLevel =>
            !_isBusy &&
            IsMainMenuReady &&
            IsGameDataReady() &&
            TryGetTargetLevel(out _);

        public string PrepareNewRecordTitle =>
            _preparedRunScore.HasValue
                ? $"New record: {_preparedRunScore.Value}"
                : "Prepare New Record";

        public string TargetLevel =>
            TryGetTargetLevel(out var level)
                ? FormatLevel(level, includeAddress: false)
                : IsGameDataReady()
                    ? "Все gameplay-уровни пройдены"
                    : "Каталог и player data ещё не готовы";

        public string Status => _status;

        public bool CanInspectFirstSession => Application.isPlaying && GameDataManager.IsLoaded &&
                                              GameDataManager.PlayerData != null;

        public bool CanGrantTutorialBonus => !_isBusy && CanInspectFirstSession && IsMainMenuReady &&
                                             !TutorialStorage.IsPlayerDataBackupActive &&
                                             !GameDataManager.IsProfileReplacementBlocked;

        public string FirstSessionState => !CanInspectFirstSession ? "Запустите игру через Bootstrap." :
            _inspectedProfile != null && (_inspectedProfile != GameDataManager.ProfileId ||
                                         _inspectedGeneration != GameDataManager.Generation)
                ? "Профиль изменён. Обновите состояние первой сессии." : _firstSessionState;

        /// <summary>Выдаёт стартовый бонус явной DEV-командой; повтор использует production-дедупликацию.</summary>
        public void GrantTutorialBonus()
        {
            if (!CanGrantTutorialBonus) return;
            _isBusy = true;
            try
            {
                var before = CapturePlayerExperience();
                bool levelChanged = false;
                // Игровой сервис выдаёт награду и marker внутри одной сохраняемой транзакции.
                GameDataManager.ExecuteTransaction(CheckpointReason.DeveloperResourceGranted,
                    () => levelChanged = new PlayerExperienceService()
                        .GrantExperienceForTutorialCompletion(GameDataManager.PlayerData),
                    () => PlayerExperienceService.PublishCommittedLevelChange(levelChanged));
                int awarded = CalculateGrantedExperience(before, CapturePlayerExperience());
                if (awarded > 0) FirstSessionTelemetry.Record("tutorial_bonus_committed", "developer_action", awarded);
                UIManager.OnRepaintScreen?.Invoke();
                SetStatus($"Tutorial bonus: +{awarded} XP. " +
                    (awarded == 0 ? "Бонус уже получен; повторная выдача заблокирована." : "Выдача сохранена."));
                InspectFirstSessionState();
            }
            catch (Exception exception)
            {
                SetStatus($"Ошибка tutorial reward: {exception.Message}", LogType.Error);
            }
            finally
            {
                _isBusy = false;
                Changed?.Invoke();
            }
        }

        /// <summary>Читает сохранённое состояние первой сессии и scoped weekly-кеш без изменения профиля.</summary>
        public void InspectFirstSessionState()
        {
            if (!CanInspectFirstSession) return;
            try
            {
                // Снимок показывает игровые флаги отдельно от presentation cursor.
                var data = GameDataManager.PlayerData;
                _inspectedProfile = GameDataManager.ProfileId;
                _inspectedGeneration = GameDataManager.Generation;
                _firstSessionState =
                    $"Снимок {DateTime.Now:HH:mm:ss}\n" +
                    $"Tutorial: completed={data.IsTutorialCompleted}, skipped={data.IsTutorialSkipped}, " +
                    $"bonus={data.HasReceivedTutorialExperience}, backup={TutorialStorage.IsPlayerDataBackupActive}\n" +
                    $"Level={data.PlayerLevel}, XP={data.ExperiencePoints}/{PlayerExperienceService.PlayerLevelThreshold}, " +
                    $"DP={data.DevelopmentPoints}\n" +
                    $"Level Up: acknowledged={data.LastAcknowledgedPlayerLevel}, " +
                    $"pending={Math.Max(0, data.PlayerLevel - Math.Max(1, data.LastAcknowledgedPlayerLevel))}\n" +
                    $"Shield lesson: started={data.IsShieldTutorialStarted}, used={data.HasUsedTutorialShield}\n" +
                    $"Return: screen={data.FirstSessionReturnScreen}, level={data.FirstSessionReturnLevel}, " +
                    $"location={data.FirstSessionReturnLocation}, part={data.FirstSessionReturnPart}";

                // Чтение coordinator не запускает сеть, отправку результата или acknowledgment.
                var coordinator = WeeklyLeaderboardCoordinator.Instance;
                if (coordinator == null)
                    _firstSessionState += "\nWeekly: coordinator ещё не готов.";
                else
                {
                    _firstSessionState += $"\nWeekly: pending presentations={coordinator.GetPendingRecordNotifications().Count}";
                    if (LevelManager.TryGetCurrentProgressKey(out var key))
                    {
                        var context = coordinator.CaptureRunContext(key);
                        var baseline = context?.PersonalBest;
                        string best = baseline == null ? "unknown" : !baseline.HadEntry ? "no entry" : baseline.Score.ToString();
                        _firstSessionState += $"\nBaseline: board={context?.LeaderboardId}, week={context?.VersionId}, " +
                            $"best={best}, fetched={baseline?.FetchedAtUtc}";
                    }
                }
            }
            catch (Exception exception)
            {
                _firstSessionState = $"Не удалось прочитать состояние: {exception.Message}";
            }
            Changed?.Invoke();
        }

        /// <summary>Готовит score на 10 больше реального weekly best текущего target.</summary>
        public async void PrepareNewRecord()
        {
            if (!CanPrepareNewRecord ||
                !TryGetTargetLevel(out var targetLevel))
            {
                return;
            }

            // Фиксируем target и блокируем команды на время чтения реального leaderboard.
            _isBusy = true;
            var operationVersion = ++_operationVersion;
            var owner = GameDataManager.OwnerPlayerId;
            var profile = GameDataManager.ProfileId;
            var generation = GameDataManager.Generation;
            SetStatus(
                $"Читается weekly best для {FormatLevel(targetLevel)}.");

            try
            {
                // Готовим новый рекорд для location + part of day target-уровня.
                var progressKey = CreateProgressKey(targetLevel);
                var coordinator = EnsureCoordinator();
                var results = await coordinator.GetResultsAsync(progressKey.LocationId, progressKey.PartOfDayId);
                if (!IsCurrentOperation(operationVersion, owner, profile, generation)) return;
                var weeklyBest = results.CurrentPlayer == null ? 0 : checked((int)results.CurrentPlayer.Score);
                _preparedRunScore = checked(weeklyBest + 10);
                SetStatus(
                    $"New record prepared: {_preparedRunScore.Value}. " +
                    $"Previous weekly best: {weeklyBest}.");
            }
            catch (Exception exception)
            {
                if (!IsCurrentOperation(operationVersion, owner, profile, generation)) return;
                SetStatus($"Ошибка: {exception.Message}", LogType.Error);
            }
            finally
            {
                if (operationVersion == _operationVersion)
                {
                    _isBusy = false;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Тихо завершает target с тремя звёздами и подготовленным либо случайным score.</summary>
        public void CompleteNextLevel()
        {
            if (!CanCompleteNextLevel ||
                !TryGetTargetLevel(out var level))
            {
                return;
            }

            // Фиксируем настройки завершения для текущего target.
            Assets.Scripts.Diagnostics.EconomyTelemetry.MarkDevelopment("complete_next_level");
            _isBusy = true;
            var runScore = _preparedRunScore ??
                           UnityEngine.Random.Range(
                               0,
                               MaxRandomRunScore + 1);
            var levelCompleted = false;
            SetStatus(
                $"Завершается {FormatLevel(level)}: 3 stars, score={runScore}.");

            try
            {
                // Контекст серверной недели относится к началу этой попытки, включая офлайн.
                var coordinator = EnsureCoordinator();
                var context = coordinator.CaptureRunContext(CreateProgressKey(level));
                if (context == null)
                    throw new InvalidOperationException("Контекст недельного результата ещё не готов.");
                // Запоминаем XP до штатных rewards.
                var beforeCompletion = CapturePlayerExperience();

                // Общий runtime-контракт обновляет stars, XP и level-completed checkpoint.
                if (!LevelManager.CompleteLevel(level.Address, 3) ||
                    LevelManager.GetLevelStars(level.Address) != 3)
                {
                    throw new InvalidOperationException(
                        "Штатный level-completed путь не записал 3 stars.");
                }

                levelCompleted = true;
                var afterStars = CapturePlayerExperience();
                var starsExperienceReward = CalculateGrantedExperience(
                    beforeCompletion,
                    afterStars);
                UIManager.OnRepaintScreen?.Invoke();

                // Production FIFO сохраняет запись локально и сама применяет подтверждённый XP.
                var run = coordinator.QueueSuccessfulRun(context, runScore);
                if (run == null)
                    throw new InvalidOperationException("Уровень сохранён; контекст результата сменился до постановки в очередь.");
                _trackedRuns[run.RunId] = (level, starsExperienceReward, beforeCompletion,
                    context.OwnerPlayerId, context.ProfileId, context.Generation);
                HandleRunChanged(run);
            }
            catch (Exception exception)
            {
                UIManager.OnRepaintScreen?.Invoke();
                SetStatus($"Ошибка: {exception.Message}", LogType.Error);
            }
            finally
            {
                // Успешно завершённый level освобождает target и подготовленный record.
                if (levelCompleted)
                    ResetTarget();

                _isBusy = false;
                Changed?.Invoke();
            }
        }

        /// <summary>Сбрасывает transient-статус после остановки Play Mode.</summary>
        public void HandlePlayModeStopped()
        {
            ClearSubscriptions();
            _isBusy = false;
            ResetTarget();
            SetStatus("Play Mode остановлен.");
        }

        /// <summary>Обновляет transient-статус после запуска Play Mode.</summary>
        public void HandlePlayModeStarted()
        {
            ClearSubscriptions();
            _isBusy = false;
            ResetTarget();
            SetStatus("Play Mode готов. Откройте Main Menu.");
        }

        /// <summary>Подписывает DEV-экран на тот же coordinator, который обрабатывает обычные забеги.</summary>
        private WeeklyLeaderboardCoordinator EnsureCoordinator()
        {
            var coordinator = WeeklyLeaderboardCoordinator.Instance ??
                throw new InvalidOperationException("Очередь недельных результатов ещё не готова.");
            if (_coordinator == coordinator) return coordinator;

            // Замена runtime-сессии освобождает старый источник событий.
            if (_coordinator != null) _coordinator.RunChanged -= HandleRunChanged;
            _trackedRuns.Clear();
            _coordinator = coordinator;
            _coordinator.RunChanged += HandleRunChanged;
            GameDataManager.ProfileChanged -= HandleProfileChanged;
            GameDataManager.ProfileChanged += HandleProfileChanged;
            return coordinator;
        }

        /// <summary>Отражает статус конкретного runId и только уже сохранённое начисление XP.</summary>
        private void HandleRunChanged(WeeklyLeaderboardRun run)
        {
            if (run == null || !_trackedRuns.TryGetValue(run.RunId, out var tracked)) return;
            if (!Application.isPlaying || GameDataManager.OwnerPlayerId != tracked.Owner ||
                GameDataManager.ProfileId != tracked.Profile || GameDataManager.Generation != tracked.Generation)
            {
                _trackedRuns.Remove(run.RunId);
                return;
            }

            // Receipt принадлежит production checkpoint; DEV tool только читает его.
            var rewardApplied = GameDataManager.PlayerData.AppliedWeeklyRewardRunIds.Contains(run.RunId);
            SetStatus(BuildCompletedStatus(tracked.Level, run, tracked.StarsExperience, rewardApplied,
                tracked.BeforeCompletion, CapturePlayerExperience()));
            UIManager.OnRepaintScreen?.Invoke();
            if (run.Status == WeeklyRunStatus.LocalOnly || run.Status == WeeklyRunStatus.NotImproved ||
                run.Status == WeeklyRunStatus.Expired || run.Status == WeeklyRunStatus.Unconfirmed || rewardApplied)
                _trackedRuns.Remove(run.RunId);
        }

        private bool IsCurrentOperation(int version, string owner, string profile, long generation) =>
            Application.isPlaying && version == _operationVersion && GameDataManager.OwnerPlayerId == owner &&
            GameDataManager.ProfileId == profile && GameDataManager.Generation == generation;

        private void HandleProfileChanged()
        {
            ++_operationVersion;
            _trackedRuns.Clear();
            _isBusy = false;
            ResetTarget();
            SetStatus("Профиль изменён. Выберите следующую проверку.");
        }

        private void ClearSubscriptions()
        {
            ++_operationVersion;
            _inspectedProfile = null;
            _firstSessionState = "Нажмите «Обновить состояние первой сессии».";
            if (_coordinator != null) _coordinator.RunChanged -= HandleRunChanged;
            _coordinator = null;
            GameDataManager.ProfileChanged -= HandleProfileChanged;
            _trackedRuns.Clear();
        }

        private bool TryGetTargetLevel(
            out LevelProgress level)
        {
            level = default;
            if (!IsGameDataReady())
                return false;

            // Сохраняем один target между Prepare и Complete.
            var levels = LevelSelectionModel.Create().FlattenedLevels;
            if (!string.IsNullOrWhiteSpace(_targetLevelAddress))
            {
                level = levels.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Address?.Trim(),
                        _targetLevelAddress,
                        StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(level.Address))
                    return true;
            }

            // После completion выбираем следующий уровень без звёзд в порядке каталога.
            level = levels.FirstOrDefault(candidate =>
                    LevelManager.GetLevelStars(candidate.Address) == 0);
            _targetLevelAddress = level.Address?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(level.Address);
        }

        private static bool IsGameDataReady()
        {
            return GameDataManager.PlayerData != null &&
                   LevelSelectionModel.Create().FlattenedLevels.Count > 0;
        }

        private static bool IsMainMenuShown()
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    "Menu",
                    StringComparison.Ordinal))
            {
                return false;
            }

            // Ищем реально отображаемый Home screen без перекрывающего modal.
            var visibleRoots = UnityEngine.Object
                .FindObjectsByType<UIDocument>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Select(document => document?.rootVisualElement)
                .Where(root => root != null)
                .ToList();
            return visibleRoots
                       .Select(root => root.Q<VisualElement>("homescreen"))
                       .Any(IsElementShown) &&
                   !visibleRoots
                       .Select(root => root.Q<VisualElement>("modal__container"))
                       .Any(IsElementShown);
        }

        private static bool IsElementShown(VisualElement element)
        {
            if (element?.panel == null)
                return false;

            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }

            return true;
        }

        private static string FormatLevel(
            LevelProgress level,
            bool includeAddress = true)
        {
            var model = LevelSelectionModel.Create();
            var location = model.Locations.FirstOrDefault(candidate =>
                candidate.Index == level.LocationIndex);
            var part = location?.Parts.FirstOrDefault(candidate =>
                candidate.Index == level.PartIndex);
            var title = $"{location?.DisplayName ?? level.LocationId} / " +
                        $"{part?.DisplayName ?? level.PartOfDayId} / " +
                        $"Level {level.LevelIndex + 1}";
            return includeAddress
                ? $"{title} ({level.Address?.Trim()})"
                : title;
        }

        private static LevelProgressKey CreateProgressKey(
            LevelProgress level)
        {
            return new LevelProgressKey(
                level.LocationId,
                level.PartOfDayId,
                level.LevelIndex);
        }

        private static string BuildCompletedStatus(
            LevelProgress level,
            WeeklyLeaderboardRun run,
            int starsExperienceReward,
            bool recordRewardApplied,
            PlayerExperienceSnapshot beforeCompletion,
            PlayerExperienceSnapshot afterCompletion)
        {
            // Показываем завершённый level и фактическую XP-награду за stars.
            var result =
                $"Level Completed: {FormatLevel(level, includeAddress: false)}\n\n" +
                $"1. First Win + improved stars — +{starsExperienceReward} XP";

            // Состояние доставки отделено от уже применённого игрового вознаграждения.
            var weeklyStatus = run.Status switch
            {
                WeeklyRunStatus.Pending => "Ожидает отправки",
                WeeklyRunStatus.AwaitingLocalSave => "Ожидает повторной записи на устройство",
                WeeklyRunStatus.LocalOnly => "Только на устройстве: серверная неделя до попытки неизвестна",
                WeeklyRunStatus.ConfirmedImprovement => "Рекорд подтверждён",
                WeeklyRunStatus.NotImproved => "Прежний рекорд не улучшен",
                WeeklyRunStatus.Expired => "Исходная серверная неделя завершилась",
                WeeklyRunStatus.Unconfirmed => "Подтверждение отправки не доказано",
                _ => run.Status.ToString()
            };
            result += $"\n2. Weekly: {weeklyStatus}\nRun ID: {run.RunId}" +
                      $"\nXP за рекорд: {(recordRewardApplied ? "начислен, receipt сохранён" : "не начислен")}";

            // Между callbacks могли пройти другие действия; показываем текущее состояние без ложной суммы.
            return result +
                   "\n\n" +
                   $"Player Level {beforeCompletion.PlayerLevel}, " +
                   $"{beforeCompletion.ExperiencePoints} XP → " +
                   $"Player Level {afterCompletion.PlayerLevel}, " +
                   $"{afterCompletion.ExperiencePoints} XP";
        }

        private static PlayerExperienceSnapshot CapturePlayerExperience()
        {
            var playerData = GameDataManager.PlayerData ??
                throw new InvalidOperationException(
                    "Player data недоступны для чтения XP.");
            return new PlayerExperienceSnapshot(
                playerData.PlayerLevel,
                playerData.ExperiencePoints);
        }

        private static int CalculateGrantedExperience(
            PlayerExperienceSnapshot before,
            PlayerExperienceSnapshot after)
        {
            var grantedExperience = checked(
                after.TotalExperience - before.TotalExperience);
            if (grantedExperience < 0)
            {
                throw new InvalidOperationException(
                    "XP уменьшился во время штатного reward.");
            }

            return grantedExperience;
        }

        private void ResetTarget()
        {
            _targetLevelAddress = string.Empty;
            _preparedRunScore = null;
        }

        private void SetStatus(
            string status,
            LogType logType = LogType.Log)
        {
            _status = status;
            if (logType == LogType.Error)
                Debug.LogError($"{ConsoleLogTag} {status}");
            else
                Debug.Log($"{ConsoleLogTag} {status}");

            Changed?.Invoke();
        }
    }
}
#endif
