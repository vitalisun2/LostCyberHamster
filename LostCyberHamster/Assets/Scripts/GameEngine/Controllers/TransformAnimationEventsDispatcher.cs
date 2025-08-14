using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    [RequireComponent(typeof(Animation))]
    internal class TransformAnimationEventsDispatcher : MonoBehaviour
    {
        public event Action<string> OnEvent;

        public void ReceiveEvent(string animEvent)
        {
            OnEvent?.Invoke(animEvent);
        }
    }
}
