using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialUiAutomationDriver
    {
        private const int _clickDelayMs = 5000;
        private static int _scheduleVersion;

        public static void DispatchClickIfEnabled(TutorialUiAction action, VisualElement target)
        {
            var shouldAutoPlay = TutorialAutomationSettings.ShouldAutoPlay();
            if (!shouldAutoPlay || target == null || target.panel == null)
            {
                DebugManager.DiagStability(
                    $"[TUTORIAL UI] automation skipped shouldAutoPlay={shouldAutoPlay} targetNull={target == null} panelNull={target?.panel == null}");
                return;
            }

            var unityContext = SynchronizationContext.Current;
            if (unityContext == null)
            {
                DebugManager.DiagStability($"[TUTORIAL UI] automation skipped missing unity context target={target.name}");
                return;
            }

            var scheduleVersion = ++_scheduleVersion;
            DispatchAfterDelayAsync(unityContext, action, target, scheduleVersion);
        }

        private static async void DispatchAfterDelayAsync(
            SynchronizationContext unityContext,
            TutorialUiAction action,
            VisualElement target,
            int scheduleVersion)
        {
            await Task.Delay(_clickDelayMs).ConfigureAwait(false);
            unityContext.Post(_ => Dispatch(action, target, scheduleVersion), null);
        }

        private static void Dispatch(TutorialUiAction action, VisualElement target, int scheduleVersion)
        {
            var shouldAutoPlay = TutorialAutomationSettings.ShouldAutoPlay();
            if (!shouldAutoPlay
                || scheduleVersion != _scheduleVersion
                || TutorialMetaCoordinator.CurrentAction != action
                || target == null
                || target.panel == null)
            {
                DebugManager.DiagStability(
                    $"[TUTORIAL UI] automation dispatch skipped shouldAutoPlay={shouldAutoPlay} " +
                    $"stale={scheduleVersion != _scheduleVersion} currentAction={TutorialMetaCoordinator.CurrentAction} " +
                    $"scheduledAction={action} targetNull={target == null} panelNull={target?.panel == null}");
                return;
            }

            DebugManager.DiagStability($"[TUTORIAL UI] automation execute action={action} target={target.name}");
            DispatchPointerClick(target);
        }

        private static void DispatchPointerClick(VisualElement target)
        {
            var position = target.worldBound.center;
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
