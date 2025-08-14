using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UltaChargeMechanics
    {
        private readonly AtomicEvent _destroyObstacleEvent;
        private readonly AtomicVariable<int> _ultaChargeAmount;

        public UltaChargeMechanics(AtomicEvent destroyObstacleEvent, AtomicVariable<int> ultaChargeAmount)
        {
            _destroyObstacleEvent = destroyObstacleEvent;
            _ultaChargeAmount = ultaChargeAmount;
        }

        public void OnEnable()
        {
            _destroyObstacleEvent.Subscribe(OnJumpOnEvent);
        }

        public void OnDisable() {
            _destroyObstacleEvent.Unsubscribe(OnJumpOnEvent);
        }

        private void OnJumpOnEvent()
        {
            var ultaToAdd = Mathf.Min(SkinManager.CurrentSkin.UltaCharge, 100 - _ultaChargeAmount.Value);
            _ultaChargeAmount.Value += ultaToAdd;

            if (_ultaChargeAmount.Value == 100 && ultaToAdd > 0)
            {
                GameEventsManager.UltaActivated();
            }
        }
    }
}

