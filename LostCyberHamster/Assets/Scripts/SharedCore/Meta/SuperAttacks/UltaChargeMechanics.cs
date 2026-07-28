using Atomic.Elements;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Vues.GameCore
{
    public class UltaChargeMechanics
    {
        private readonly AtomicEvent<Obstacle> _destroyObstacleEvent;
        private readonly AtomicVariable<int> _ultaChargeAmount;
        private readonly int _chargePerObstacle;

        public UltaChargeMechanics(
            AtomicEvent<Obstacle> destroyObstacleEvent,
            AtomicVariable<int> ultaChargeAmount,
            int chargePerObstacle)
        {
            _destroyObstacleEvent = destroyObstacleEvent;
            _ultaChargeAmount = ultaChargeAmount;
            _chargePerObstacle = chargePerObstacle;
        }

        public void OnEnable()
        {
            _destroyObstacleEvent.Subscribe(OnJumpOnEvent);
        }

        public void OnDisable()
        {
            _destroyObstacleEvent.Unsubscribe(OnJumpOnEvent);
        }

        private void OnJumpOnEvent(Obstacle destroyedObstacle)
        {
            if (_chargePerObstacle <= 0)
            {
                return;
            }

            var ultaToAdd = Mathf.Min(_chargePerObstacle, 100 - _ultaChargeAmount.Value);
            _ultaChargeAmount.Value += ultaToAdd;

            if (_ultaChargeAmount.Value == 100 && ultaToAdd > 0)
            {
                GameEventsManager.UltaActivated();
            }
        }
    }
}
