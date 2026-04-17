using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot
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
            DebugManager.DiagLog(
                $"[BotV2 DAMAGE] lives={_hamster.Lives.Value} " +
                $"lane={(_hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
                $"state={_hamster.HamsterState.Value}");

            if (_hamster.Lives.Value <= 0)
            {
                DebugManager.DiagLog("[TEST RESULT] FAIL");
                DebugManager.DiagStability("[TEST RESULT] FAIL");
            }
        }

        private void OnGameFinished()
        {
            DebugManager.DiagLog(
                $"[TEST FINISH] state={_gameManager.State} " +
                $"lives={_hamster.Lives.Value}");
        }

        private static void OnLevelCompleted(int levelId, int stars)
        {
            DebugManager.DiagLog($"[TEST RESULT] WIN level={levelId} stars={stars}");
            DebugManager.DiagStability($"[TEST RESULT] WIN level={levelId} stars={stars}");
        }
    }
}
