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

        /// <summary>
        /// Подписывается на события урона и завершения уровня.
        /// </summary>
        public RuntimeBotEventTracker(Hamster hamster, GameManager gameManager)
        {
            _hamster = hamster;
            _gameManager = gameManager;

            _hamster.DamageEvent.Subscribe(OnDamage);
            _gameManager.OnFinish += OnGameFinished;
            GameEventsManager.OnLevelCompleted += OnLevelCompleted;
        }

        /// <summary>
        /// Снимает runtime-подписки трекера.
        /// </summary>
        public void Dispose()
        {
            if (_hamster != null)
                _hamster.DamageEvent.Unsubscribe(OnDamage);

            if (_gameManager != null)
                _gameManager.OnFinish -= OnGameFinished;

            GameEventsManager.OnLevelCompleted -= OnLevelCompleted;
        }

        private void OnDamage()
        {
            if (_hamster.Lives.Value <= 0)
            {
            }
        }

        private void OnGameFinished()
        {
        }

        private static void OnLevelCompleted(int levelId, int stars)
        {
        }
    }
}
