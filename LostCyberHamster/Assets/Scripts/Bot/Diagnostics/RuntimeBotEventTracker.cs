using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Пишет в диагностику ключевые runtime-события, важные для тестов бота.
    /// </summary>
    public sealed class RuntimeBotEventTracker : IDisposable
    {
        private readonly Hamster _hamster;
        private readonly GameManager _gameManager;
        private readonly bool _isTestLevelRun;
        private int _lastEnergy;

        /// <summary>
        /// Подписывается на события завершения уровня.
        /// </summary>
        public RuntimeBotEventTracker(Hamster hamster, GameManager gameManager)
        {
            _hamster = hamster;
            _gameManager = gameManager;
            _isTestLevelRun = AutomationRuntimePrefs.IsTestLevelAutomationRun();
            _lastEnergy = _hamster.Energy.Value;

            _gameManager.OnFinish += OnGameFinished;
            GameEventsManager.OnLevelCompleted += OnLevelCompleted;
            GameEventsManager.OnEnergyAdded += OnEnergyAdded;
            GameEventsManager.OnEnergySpent += OnEnergySpent;
            _hamster.Energy.Subscribe(OnEnergyChanged);

            BotRuntimeEventDiagnostics.LogEnergyStart(_lastEnergy);
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
            BotRuntimeEventDiagnostics.LogTestFinish(_gameManager, _hamster);

            if (_isTestLevelRun && _hamster.Lives.Value > 0)
                BotRuntimeEventDiagnostics.LogTestLevelPassed();
        }

        private static void OnLevelCompleted(int levelId, int stars)
        {
            BotRuntimeEventDiagnostics.LogLevelCompleted(levelId, stars);
        }

        private void OnEnergyChanged(int energy)
        {
            int delta = energy - _lastEnergy;
            _lastEnergy = energy;
            BotRuntimeEventDiagnostics.LogEnergyChanged(delta, energy);
        }

        private void OnEnergyAdded(int amount)
        {
            BotRuntimeEventDiagnostics.LogEnergyAdded(amount, _hamster.Energy.Value);
        }

        private void OnEnergySpent(int amount)
        {
            BotRuntimeEventDiagnostics.LogEnergySpent(amount, _hamster.Energy.Value);
        }
    }
}
