using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Отправляет отложенный штатный pointer click в текущую UI-цель tutorial.
    /// </summary>
    internal sealed class TutorialUiAutomation : IDisposable
    {
        private const int _clickDelayMs = 5000;

        private int _scheduleVersion;
        private bool _isDisposed;

        public void Schedule(VisualElement target)
        {
            Cancel();
            if (_isDisposed || !TutorialAutomation.ShouldAutoPlay() || target?.panel == null)
            {
                return;
            }

            SynchronizationContext unityContext = SynchronizationContext.Current;
            if (unityContext == null)
            {
                return;
            }

            int scheduleVersion = _scheduleVersion;
            DispatchAfterDelayAsync(unityContext, target, scheduleVersion);
        }

        public void Cancel()
        {
            _scheduleVersion++;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Cancel();
            _isDisposed = true;
        }

        private async void DispatchAfterDelayAsync(
            SynchronizationContext unityContext,
            VisualElement target,
            int scheduleVersion)
        {
            await Task.Delay(_clickDelayMs).ConfigureAwait(false);
            unityContext.Post(_ => Dispatch(target, scheduleVersion), null);
        }

        private void Dispatch(VisualElement target, int scheduleVersion)
        {
            if (_isDisposed
                || scheduleVersion != _scheduleVersion
                || !TutorialAutomation.ShouldAutoPlay()
                || target?.panel == null)
            {
                return;
            }

            Vector2 position = target.worldBound.center;
            using var pointerDown = PointerDownEvent.GetPooled(CreateMouseEvent(EventType.MouseDown, position));
            target.SendEvent(pointerDown);

            using var pointerUp = PointerUpEvent.GetPooled(CreateMouseEvent(EventType.MouseUp, position));
            target.SendEvent(pointerUp);
        }

        private static Event CreateMouseEvent(EventType eventType, Vector2 position)
        {
            return new Event
            {
                type = eventType,
                mousePosition = position,
                button = 0,
                clickCount = 1
            };
        }
    }
}
