using System;
using Atomic.Elements;
using UnityEngine;

namespace Vues.GameCore
{
    public class UltaMechanics
    {
        private readonly AtomicEvent _ultaEvent;
        private readonly AtomicVariable<int> _ultaChargeAmount;
        private readonly Func<bool> _applyAttack;
        private readonly Action _updateAttack;

        public UltaMechanics(
            AtomicEvent ultaEvent,
            AtomicVariable<int> ultaChargeAmount,
            Func<bool> applyAttack,
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
            // Проверяем наличие runtime и полный заряд.
            if (_applyAttack == null)
            {
                return;
            }

            if (_ultaChargeAmount.Value < 100)
            {
                Debug.LogWarning("Ulta charge is not full");
                return;
            }

            // Списываем заряд только после успешной активации runtime.
            if (!_applyAttack.Invoke())
            {
                return;
            }

            _ultaChargeAmount.Value = 0;
            GameEventsManager.UltaUsed();
        }
    }
}
