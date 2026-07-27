#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>Передаёт mouse/touch drag из uGUI в callback владельца DEV-окна.</summary>
    internal sealed class DevToolsPointerDragHandle :
        MonoBehaviour,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        private Action<Vector2> _dragged;
        private int _activePointerId = NoPointer;

        public void Configure(Action<Vector2> dragged)
        {
            _dragged = dragged;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_activePointerId == NoPointer)
                _activePointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId ||
                eventData.delta.sqrMagnitude <= 0f)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? Mathf.Max(canvas.scaleFactor, 0.001f) : 1f;
            _dragged?.Invoke(eventData.delta / scaleFactor);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ReleasePointer(eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleasePointer(eventData.pointerId);
        }

        private void OnDisable()
        {
            _activePointerId = NoPointer;
        }

        private void ReleasePointer(int pointerId)
        {
            if (_activePointerId == pointerId)
                _activePointerId = NoPointer;
        }
    }
}
#endif
