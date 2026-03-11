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
            OnEvent?.Invoke(animEvent);
        }
    }
}
