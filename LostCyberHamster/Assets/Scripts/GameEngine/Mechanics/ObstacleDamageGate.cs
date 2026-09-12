using Assets.Scripts.Gameplay;
using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Единая точка применения contact-damage и shield-защиты.
    /// </summary>
    public sealed class ObstacleDamageGate
    {
        private readonly AtomicVariable<bool> _isProtected;
        private readonly AtomicVariable<bool> _isSuperAttackDestructiveOnCollision;
        private readonly AtomicEvent<Obstacle> _protectedContactEvent;
        private readonly AtomicEvent _damageEvent;
        private readonly AtomicEvent<Obstacle> _destroyObstacleBySuperAttackEvent;

        public ObstacleDamageGate(
            AtomicVariable<bool> isProtected,
            AtomicVariable<bool> isSuperAttackDestructiveOnCollision,
            AtomicEvent<Obstacle> protectedContactEvent,
            AtomicEvent damageEvent,
            AtomicEvent<Obstacle> destroyObstacleBySuperAttackEvent)
        {
            _isProtected = isProtected;
            _isSuperAttackDestructiveOnCollision = isSuperAttackDestructiveOnCollision;
            _protectedContactEvent = protectedContactEvent;
            _damageEvent = damageEvent;
            _destroyObstacleBySuperAttackEvent = destroyObstacleBySuperAttackEvent;
        }

        public void HandleContact(Obstacle obstacle)
        {
            // Pooled obstacle уже снят со сцены и не должен повторно участвовать в damage pipeline.
            if (obstacle != null && !obstacle.isActiveAndEnabled)
                return;

            if (_isProtected.Value)
            {
                if (obstacle != null)
                    _protectedContactEvent.Invoke(obstacle);
            }
            else if (obstacle == null || obstacle.TryMarkContactDamageDealt())
            {
                _damageEvent.Invoke();
            }

            if (_isSuperAttackDestructiveOnCollision.Value && obstacle != null)
            {
                _destroyObstacleBySuperAttackEvent.Invoke(obstacle);
            }
        }
    }
}