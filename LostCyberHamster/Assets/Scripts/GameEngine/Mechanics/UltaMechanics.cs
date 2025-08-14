using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UltaMechanics
    {
        private readonly AtomicEvent _ultaEvent;
        private readonly AtomicVariable<int> _ultaChargeAmount;

        public UltaMechanics(AtomicEvent ultaEvent, AtomicVariable<int> ultaChargeAmount)
        {
            _ultaEvent = ultaEvent;
            _ultaChargeAmount = ultaChargeAmount;
        }

        public void OnUpdate()
        {
           SkinManager.CurrentSkin.UpdateUlta();
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
            Debug.Log("Ulta event received");

            if (_ultaChargeAmount.Value < 100)
            {
                Debug.LogWarning("Ulta charge is not full");
                return;
            }

            _ultaChargeAmount.Value = 0;

            SkinManager.CurrentSkin.ApplyUlta();
            GameEventsManager.UltaUsed();
        }
    }
}
