using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using Atomic.Elements;
using GameManagement.Leaderboard;
using GameManagement.Progress;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Передаёт результат успешного забега в локальный расчёт и Unity Leaderboards.
    /// </summary>
    public sealed class PartOfDayScoreMechanics
    {
        private readonly RunScoreMechanics _runScoreMechanics;
        private readonly AtomicVariable<int> _lives;
        private readonly GameManager _gameManager;
        private readonly LeaderboardService _leaderboardService = new();

        public RunResultData LatestResult { get; private set; }

        public event Action<RunResultData> ResultChanged;

        public PartOfDayScoreMechanics(
            RunScoreMechanics runScoreMechanics,
            AtomicVariable<int> lives,
            GameManager gameManager)
        {
            _runScoreMechanics = runScoreMechanics;
            _lives = lives;
            _gameManager = gameManager;
        }

        public void OnEnable()
        {
            _gameManager.OnFinish += OnFinish;
        }

        public void OnDisable()
        {
            _gameManager.OnFinish -= OnFinish;
        }

        private void OnFinish()
        {
            if (_lives.Value <= 0)
            {
                return;
            }

            if (!LevelManager.TryGetCurrentProgressKey(out var progressKey))
            {
                Debug.LogWarning("[PartOfDayScore] Current level key could not be resolved; run ignored.");
                return;
            }

            var runScore = _runScoreMechanics.CurrentScore;

            // Сохраняем локальный результат и сразу открываем состояние загрузки.
            PartOfDayScoreService.RecordSuccessfulRun(progressKey, runScore);
            var pendingResult = new RunResultData(
                progressKey,
                runScore,
                0,
                0,
                false,
                IsLastLevelOfPart(progressKey),
                RunResultSubmissionState.Pending);
            PublishResult(pendingResult);

            // Получаем авторитетный недельный рекорд из Unity Leaderboards.
            _ = SubmitScoreAsync(pendingResult);
        }

        private async Task SubmitScoreAsync(RunResultData pendingResult)
        {
            try
            {
                var submission = await _leaderboardService.SubmitSuccessfulRunAsync(
                    pendingResult.LevelKey,
                    pendingResult.RunScore);
                PublishResult(new RunResultData(
                    pendingResult.LevelKey,
                    pendingResult.RunScore,
                    submission.LevelBestScore,
                    submission.PartOfDayTotalScore,
                    submission.IsNewRecord,
                    pendingResult.IsLastLevelOfPart,
                    submission.WasSubmitted
                        ? RunResultSubmissionState.Submitted
                        : RunResultSubmissionState.NotRequired));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Leaderboard] Score submission failed: {exception.Message}");
                PublishResult(new RunResultData(
                    pendingResult.LevelKey,
                    pendingResult.RunScore,
                    0,
                    0,
                    false,
                    pendingResult.IsLastLevelOfPart,
                    RunResultSubmissionState.Failed));
            }
        }

        private static bool IsLastLevelOfPart(LevelProgressKey progressKey)
        {
            var levelCount = LevelManager
                .GetLevelsForPartOfDay(
                    LevelManager.GetLocationIndex(),
                    progressKey.PartOfDayId)
                .Count();
            return levelCount > 0 && progressKey.LevelIndex == levelCount - 1;
        }

        private void PublishResult(RunResultData result)
        {
            LatestResult = result;
            ResultChanged?.Invoke(result);
        }
    }
}
