using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Пишет в диагностику ключевые runtime-события, важные для тестов бота.
    /// </summary>
    public sealed class RuntimeBotEventTracker : IDisposable
    {
        private readonly Hamster _hamster;
        private readonly GameManager _gameManager;
        private int _lastEnergy;

        /// <summary>
        /// Подписывается на события завершения уровня.
        /// </summary>
        public RuntimeBotEventTracker(Hamster hamster, GameManager gameManager)
        {
            _hamster = hamster;
            _gameManager = gameManager;
            _lastEnergy = _hamster.Energy.Value;

            _gameManager.OnFinish += OnGameFinished;
            GameEventsManager.OnLevelCompleted += OnLevelCompleted;
            GameEventsManager.OnEnergyAdded += OnEnergyAdded;
            GameEventsManager.OnEnergySpent += OnEnergySpent;
            _hamster.Energy.Subscribe(OnEnergyChanged);

            DebugManager.DiagEconomy($"[Energy] start value={_lastEnergy}");
        }

        /// <summary>
        /// Снимает runtime-подписки трекера.
        /// </summary>
        public void Dispose()
        {
            if (_gameManager != null)
                _gameManager.OnFinish -= OnGameFinished;

            GameEventsManager.OnLevelCompleted -= OnLevelCompleted;
            GameEventsManager.OnEnergyAdded -= OnEnergyAdded;
            GameEventsManager.OnEnergySpent -= OnEnergySpent;
            _hamster?.Energy.Unsubscribe(OnEnergyChanged);
        }

        private void OnGameFinished()
        {
            DebugManager.DiagLog(
                $"[TEST FINISH] state={_gameManager.State} " +
                $"lives={_hamster.Lives.Value} energy={_hamster.Energy.Value}");
        }

        private static void OnLevelCompleted(int levelId, int stars)
        {
            DebugManager.DiagLog($"[TEST RESULT] WIN level={levelId} stars={stars}");
            DebugManager.DiagStability($"[TEST RESULT] WIN level={levelId} stars={stars}");
        }

        private void OnEnergyChanged(int energy)
        {
            int delta = energy - _lastEnergy;
            _lastEnergy = energy;
            DebugManager.DiagEconomy($"[Energy] change delta={delta:+#;-#;0} value={energy}");
        }

        private void OnEnergyAdded(int amount)
        {
            DebugManager.DiagEconomy($"[Energy] added amount={amount} value={_hamster.Energy.Value}");
        }

        private void OnEnergySpent(int amount)
        {
            DebugManager.DiagEconomy($"[Energy] spent amount={amount} value={_hamster.Energy.Value}");
        }
    }
}
