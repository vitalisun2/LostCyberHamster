using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameManagerLogic;
using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Управляет gameplay-длительностью damage immunity независимо от visual-клипа.
    /// </summary>
    public sealed class TakeDamageMechanics
    {
        private const float DamageDuration = 1f;

        private readonly AtomicEvent _damageEvent;
        private readonly SpriteAnimatorController _spriteAnimatorController;
        private readonly AtomicVariable<bool> _isDamaged;
        private readonly AtomicVariable<int> _lives;
        private readonly GameManager _gameManager;

        private float _remainingDamageTime;

        public TakeDamageMechanics(
            AtomicEvent damageEvent,
            SpriteAnimatorController spriteAnimatorController,
            AtomicVariable<bool> isDamaged,
            AtomicVariable<int> lives,
            GameManager gameManager)
        {
            _damageEvent = damageEvent;
            _spriteAnimatorController = spriteAnimatorController;
            _isDamaged = isDamaged;
            _lives = lives;
            _gameManager = gameManager;
        }

        public void OnEnable()
        {
            _damageEvent.Subscribe(OnDamageEvent);
        }

        public void OnDisable()
        {
            _damageEvent.Unsubscribe(OnDamageEvent);
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_isDamaged.Value || _gameManager.State != GameState.PLAYING)
                return;

            _remainingDamageTime -= deltaTime;
            if (_remainingDamageTime > 0f)
                return;

            _remainingDamageTime = 0f;
            _isDamaged.Value = false;
            _spriteAnimatorController.SetDamaged(false);
        }

        private void OnDamageEvent()
        {
            const int livesToLose = 1;
            _lives.Value -= livesToLose;
            _remainingDamageTime = DamageDuration;
            _isDamaged.Value = true;
            _spriteAnimatorController.SetDamaged(true);
            GameEventsManager.LivesLost(livesToLose);
        }
    }
}
