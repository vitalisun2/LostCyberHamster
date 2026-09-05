using System;
using System.Linq;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using Atomic.Elements;
using GameManagement.Leaderboard;
using GameManagement.Progress;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Фиксирует неделю до забега и показывает состояние durable-отправки успешного результата.
    /// </summary>
    public sealed class PartOfDayScoreMechanics
    {
        private readonly RunScoreMechanics _runScoreMechanics;
        private readonly AtomicVariable<int> _lives;
        private readonly GameManager _gameManager;
        private WeeklyLeaderboardCoordinator _coordinator;
        private WeeklyRunContext _runContext;
        private LevelProgressKey _runLevelKey;
        private string _runId;
        private bool _contextCaptured;
        private bool _hasRunLevelKey;
        private bool _isScoreSubmissionStarted;

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
            // Серверная версия фиксируется один раз, до начала gameplay.
            if (!_contextCaptured)
            {
                _contextCaptured = true;
                _coordinator = WeeklyLeaderboardCoordinator.Instance;
                _hasRunLevelKey = LevelManager.TryGetCurrentProgressKey(out _runLevelKey);
                if (_hasRunLevelKey)
                    _runContext = _coordinator?.CaptureRunContext(_runLevelKey);
            }
            if (_coordinator != null) _coordinator.RunChanged += OnRunChanged;
            _gameManager.OnFinish += OnFinish;
        }

        public void OnDisable()
        {
            _gameManager.OnFinish -= OnFinish;
            if (_coordinator != null) _coordinator.RunChanged -= OnRunChanged;
        }

        private void OnFinish()
        {
            if (_lives.Value <= 0)
            {
                return;
            }

            if (!_hasRunLevelKey)
            {
                Debug.LogWarning("[PartOfDayScore] Current level key could not be resolved; run ignored.");
                return;
            }

            if (_isScoreSubmissionStarted)
            {
                return;
            }

            _isScoreSubmissionStarted = true;
            var runScore = _runScoreMechanics.CurrentScore;
            var progressKey = _runLevelKey;

            // Победа и локальный score доступны сразу, независимо от соединения.
            var pendingResult = new RunResultData(
                progressKey,
                runScore,
                0,
                false,
                IsLastLevelOfPart(progressKey),
                RunResultSubmissionState.Pending);
            LatestResult = pendingResult;

            QueueScore(pendingResult);
        }

        private void QueueScore(RunResultData pendingResult)
        {
            try
            {
                var run = _coordinator?.QueueSuccessfulRun(_runContext, pendingResult.RunScore);
                _runId = run?.RunId;
                if (run == null)
                    PublishSubmissionState(RunResultSubmissionState.Failed);
                else
                    OnRunChanged(run);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Leaderboard] Local queue could not be saved ({exception.GetType().Name}).");
                PublishSubmissionState(RunResultSubmissionState.Failed);
            }
        }

        private void OnRunChanged(WeeklyLeaderboardRun run)
        {
            if (run.RunId != _runId || LatestResult == null) return;
            var state = run.Status switch
            {
                WeeklyRunStatus.ConfirmedImprovement => RunResultSubmissionState.Submitted,
                WeeklyRunStatus.NotImproved => RunResultSubmissionState.NotRequired,
                WeeklyRunStatus.Expired => RunResultSubmissionState.Expired,
                WeeklyRunStatus.Unconfirmed => RunResultSubmissionState.Unconfirmed,
                WeeklyRunStatus.AwaitingLocalSave => RunResultSubmissionState.Failed,
                WeeklyRunStatus.LocalOnly => RunResultSubmissionState.LocalOnly,
                _ => RunResultSubmissionState.Pending
            };
            PublishSubmissionState(state, run.WeeklyBest,
                run.Status == WeeklyRunStatus.ConfirmedImprovement);
        }

        private void PublishSubmissionState(RunResultSubmissionState state, int weeklyBest = 0, bool isRecord = false)
        {
            PublishResult(new RunResultData(LatestResult.LevelKey, LatestResult.RunScore,
                weeklyBest, isRecord, LatestResult.IsLastLevelOfPart, state));
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
