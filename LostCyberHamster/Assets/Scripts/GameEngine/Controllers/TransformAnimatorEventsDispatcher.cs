using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    [RequireComponent(typeof(Animator))]
    public sealed class TransformAnimatorEventsDispatcher : MonoBehaviour
    {
        public event Action<string> OnEvent;

        public void ReceiveEvent(string animEvent)
        {
            DebugManager.DiagLog($"[TransformAnimEvents] ReceiveEvent: '{animEvent}'");
            OnEvent?.Invoke(animEvent);
        }
    }
}
