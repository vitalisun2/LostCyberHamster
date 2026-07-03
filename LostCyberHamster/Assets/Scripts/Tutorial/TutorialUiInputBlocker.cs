using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    public sealed class TutorialUiInputBlocker
    {
        private VisualElement _root;
        private VisualElement _allowedTarget;

        public void Attach(VisualElement root, VisualElement allowedTarget)
        {
            Detach();

            _root = root;
            _allowedTarget = allowedTarget;
            _root?.RegisterCallback<PointerDownEvent>(BlockUnexpectedPointerDown, TrickleDown.TrickleDown);
        }

        public void Detach()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<PointerDownEvent>(
                    BlockUnexpectedPointerDown,
                    TrickleDown.TrickleDown);
            }

            _root = null;
            _allowedTarget = null;
        }

        private void BlockUnexpectedPointerDown(PointerDownEvent evt)
        {
            if (_allowedTarget != null && !IsEventInsideAllowedTarget(evt))
            {
                evt.StopImmediatePropagation();
            }
        }

        private bool IsEventInsideAllowedTarget(EventBase evt)
        {
            return evt.target is VisualElement visualElement
                   && IsSameOrChildOfAllowedTarget(visualElement);
        }

        private bool IsSameOrChildOfAllowedTarget(VisualElement visualElement)
        {
            while (visualElement != null)
            {
                if (visualElement == _allowedTarget)
                {
                    return true;
                }

                visualElement = visualElement.parent;
            }

            return false;
        }
    }
}
