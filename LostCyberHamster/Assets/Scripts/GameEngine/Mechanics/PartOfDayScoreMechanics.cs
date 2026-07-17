using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using Atomic.Elements;
using GameManagement.Progress;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Передаёт результат успешного забега в расчёт результата части дня.
    /// </summary>
    public sealed class PartOfDayScoreMechanics
    {
        private readonly RunScoreMechanics _runScoreMechanics;
        private readonly AtomicVariable<int> _lives;
        private readonly GameManager _gameManager;

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

            if (LevelManager.TryGetCurrentProgressKey(out var progressKey))
            {
                PartOfDayScoreService.RecordSuccessfulRun(progressKey, _runScoreMechanics.CurrentScore);
                return;
            }

            Debug.LogWarning("[PartOfDayScore] Current level key could not be resolved; run ignored.");
        }
    }
}
