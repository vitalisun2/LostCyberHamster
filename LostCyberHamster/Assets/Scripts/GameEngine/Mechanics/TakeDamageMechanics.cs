using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class TakeDamageMechanics
    {
        private AtomicEvent _damageEvent;
        private SpriteAnimatorController _spriteAnimatorController;
        private AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly AtomicVariable<bool> _isDamaged;
        private readonly AtomicVariable<int> _lives;

        public TakeDamageMechanics(
            AtomicEvent damageEvent,
            SpriteAnimatorController spriteAnimatorController,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isDamaged,
            AtomicVariable<int> lives)
        {
            _damageEvent = damageEvent;
            _spriteAnimatorController = spriteAnimatorController;
            _hamsterState = hamsterState;
            _isDamaged = isDamaged;
            _lives = lives;
        }

        public void OnEnable()
        {
            _damageEvent.Subscribe(OnDamageEvent);
        }

        public void OnDisable()
        {
            _damageEvent.Unsubscribe(OnDamageEvent);
        }

        private void OnDamageEvent()
        {
            var lifesToLost = 1;
            _lives.Value -= lifesToLost;
            _spriteAnimatorController.Blink();
            _isDamaged.Value = true;
            GameEventsManager.LivesLost(lifesToLost);
        }

    }
}
