using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Отслеживает игровые события (damage, finish, level completed) и
    /// пишет TEST RESULT маркеры для automation bridge.
    /// Позже сюда ляжет economy tracking.
    /// </summary>
    public class GameEventTracker
    {
        private readonly Hamster _hamster;
        private readonly GameManager _gameManager;

        public GameEventTracker(Hamster hamster, GameManager gameManager)
        {
            _hamster = hamster;
            _gameManager = gameManager;

            _hamster.DamageEvent.Subscribe(OnDamage);
            _gameManager.OnFinish += OnGameFinished;
            GameEventsManager.OnLevelCompleted += OnLevelCompleted;
        }

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
                $"[DAMAGE] lives={_hamster.Lives.Value} " +
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

        private void OnLevelCompleted(int levelId, int stars)
        {
            DebugManager.DiagLog($"[TEST RESULT] WIN level={levelId} stars={stars}");
            DebugManager.DiagStability($"[TEST RESULT] WIN level={levelId} stars={stars}");
        }
    }
}
