using System;
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

            PartOfDayScoreService.RecordSuccessfulRun(progressKey, runScore);
            _ = SubmitScoreAsync(progressKey, runScore);
        }

        private async Task SubmitScoreAsync(
            LevelProgressKey progressKey,
            int runScore)
        {
            try
            {
                await _leaderboardService.SubmitSuccessfulRunAsync(
                    progressKey,
                    runScore);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Leaderboard] Score submission failed: {exception.Message}");
            }
        }
    }
}
