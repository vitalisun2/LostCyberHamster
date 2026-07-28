using System;
using Atomic.Elements;
using UnityEngine;

namespace Vues.GameCore
{
    public class UltaMechanics
    {
        private readonly AtomicEvent _ultaEvent;
        private readonly AtomicVariable<int> _ultaChargeAmount;
        private readonly Action _applyAttack;
        private readonly Action _updateAttack;

        public UltaMechanics(
            AtomicEvent ultaEvent,
            AtomicVariable<int> ultaChargeAmount,
            Action applyAttack,
            Action updateAttack)
        {
            _ultaEvent = ultaEvent;
            _ultaChargeAmount = ultaChargeAmount;
            _applyAttack = applyAttack;
            _updateAttack = updateAttack;
        }

        public void OnUpdate()
        {
            _updateAttack?.Invoke();
        }

        public void OnEnable()
        {
            _ultaEvent.Subscribe(OnUltaEvent);
        }

        public void OnDisable()
        {
            _ultaEvent.Unsubscribe(OnUltaEvent);
        }

        private void OnUltaEvent()
        {
            if (_applyAttack == null)
            {
                return;
            }

            if (_ultaChargeAmount.Value < 100)
            {
                Debug.LogWarning("Ulta charge is not full");
                return;
            }

            _ultaChargeAmount.Value = 0;
            _applyAttack.Invoke();
            GameEventsManager.UltaUsed();
        }
    }
}
