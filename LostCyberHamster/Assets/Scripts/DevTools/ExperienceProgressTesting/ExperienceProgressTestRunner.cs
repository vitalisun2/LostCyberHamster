#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
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

        private readonly LeaderboardService _leaderboardService = new();
        private readonly PlayerExperienceService _playerExperienceService = new();

        private bool _isBusy;
        private int? _preparedRunScore;
        private string _targetLevelAddress = string.Empty;
        private string _status =
            "Запустите игру через Bootstrap и оставайтесь в Main Menu.";

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
            SetStatus(
                $"Читается weekly best для {FormatLevel(targetLevel)}.");

            try
            {
                // Готовим новый рекорд для location + part of day target-уровня.
                var progressKey = CreateProgressKey(targetLevel);
                var weeklyBest =
                    await _leaderboardService
                        .GetPlayerWeeklyBestRunScoreAsync(progressKey);
                _preparedRunScore = checked(weeklyBest + 10);
                SetStatus(
                    $"New record prepared: {_preparedRunScore.Value}. " +
                    $"Previous weekly best: {weeklyBest}.");
            }
            catch (Exception exception)
            {
                SetStatus($"Ошибка: {exception.Message}", LogType.Error);
            }
            finally
            {
                _isBusy = false;
                Changed?.Invoke();
            }
        }

        /// <summary>Тихо завершает target с тремя звёздами и подготовленным либо случайным score.</summary>
        public async void CompleteNextLevel()
        {
            if (!CanCompleteNextLevel ||
                !TryGetTargetLevel(out var level))
            {
                return;
            }

            // Фиксируем настройки завершения для текущего target.
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

                // Штатный leaderboard сервис проверяет record; реальный XP сервис выдаёт reward.
                var progressKey = CreateProgressKey(level);
                var submission =
                    await _leaderboardService.SubmitSuccessfulRunAsync(
                        progressKey,
                        runScore);
                var recordExperienceReward = 0;
                if (submission.IsNewRecord)
                {
                    var currentPlayerData = GameDataManager.PlayerData ??
                        throw new InvalidOperationException(
                            "Player data недоступны после проверки record.");
                    var beforeRecordReward = CapturePlayerExperience();
                    _playerExperienceService
                        .GrantExperienceForWeeklyLeaderboardRecord(
                            currentPlayerData);
                    PlayerProgressCommitter.Commit(
                        CheckpointReason.WeeklyLeaderboardRecordRewarded);
                    var afterRecordReward = CapturePlayerExperience();
                    recordExperienceReward = CalculateGrantedExperience(
                        beforeRecordReward,
                        afterRecordReward);
                }

                var afterCompletion = CapturePlayerExperience();
                UIManager.OnRepaintScreen?.Invoke();
                SetStatus(
                    BuildCompletedStatus(
                        level,
                        submission,
                        starsExperienceReward,
                        recordExperienceReward,
                        beforeCompletion,
                        afterCompletion));
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
            _isBusy = false;
            ResetTarget();
            SetStatus("Play Mode остановлен.");
        }

        /// <summary>Обновляет transient-статус после запуска Play Mode.</summary>
        public void HandlePlayModeStarted()
        {
            ResetTarget();
            SetStatus("Play Mode готов. Откройте Main Menu.");
        }

        private bool TryGetTargetLevel(
            out LevelSelectionModel.LevelReference level)
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
            LevelSelectionModel.LevelReference level,
            bool includeAddress = true)
        {
            var model = LevelSelectionModel.Create();
            var location = model.Locations.FirstOrDefault(candidate =>
                candidate.Index == level.LocationIndex);
            var part = location?.Parts.FirstOrDefault(candidate =>
                candidate.Index == level.PartIndex);
            var title = $"{location?.DisplayName ?? level.LocationId} / " +
                        $"{part?.DisplayName ?? level.PartId} / " +
                        $"Level {level.LevelIndex + 1}";
            return includeAddress
                ? $"{title} ({level.Address?.Trim()})"
                : title;
        }

        private static LevelProgressKey CreateProgressKey(
            LevelSelectionModel.LevelReference level)
        {
            return new LevelProgressKey(
                level.LocationId,
                level.PartId,
                level.LevelIndex);
        }

        private static string BuildCompletedStatus(
            LevelSelectionModel.LevelReference level,
            LeaderboardSubmissionResult submission,
            int starsExperienceReward,
            int recordExperienceReward,
            PlayerExperienceSnapshot beforeCompletion,
            PlayerExperienceSnapshot afterCompletion)
        {
            // Показываем завершённый level и фактическую XP-награду за stars.
            var result =
                $"Level Completed: {FormatLevel(level, includeAddress: false)}\n\n" +
                $"1. 3 Stars Earned — +{starsExperienceReward} XP";

            // Добавляем record только после подтверждения реальным leaderboard.
            if (submission.IsNewRecord)
            {
                result +=
                    "\n2. Weekly Record Updated: " +
                    $"{submission.PreviousWeeklyBestRunScore} → " +
                    $"{submission.WeeklyBestRunScore} — " +
                    $"+{recordExperienceReward} XP";
            }

            // Завершаем ledger суммой причин и реальным состоянием Player Level/XP.
            var totalExperienceReward = checked(
                starsExperienceReward + recordExperienceReward);
            return result +
                   $"\nTotal: +{totalExperienceReward} XP\n\n" +
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
