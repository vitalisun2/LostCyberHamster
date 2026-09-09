using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Обводит существующее управление отдельным слоем, сохраняя его стиль, фокус и обработчики.</summary>
    internal sealed class FirstSessionFocusOutline : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _outline;

        public FirstSessionFocusOutline(VisualElement root)
        {
            _root = root;
            _outline = new VisualElement { name = "first-session-focus", pickingMode = PickingMode.Ignore };
            _outline.style.position = Position.Absolute;
            _outline.style.borderLeftWidth = _outline.style.borderRightWidth = 3;
            _outline.style.borderTopWidth = _outline.style.borderBottomWidth = 3;
            var color = new Color(1f, .84f, .2f);
            _outline.style.borderLeftColor = _outline.style.borderRightColor = color;
            _outline.style.borderTopColor = _outline.style.borderBottomColor = color;
            _outline.style.borderTopLeftRadius = _outline.style.borderTopRightRadius = 8;
            _outline.style.borderBottomLeftRadius = _outline.style.borderBottomRightRadius = 8;
            _root.Add(_outline);
            Hide();
        }

        public bool Show(VisualElement target)
        {
            if (target?.panel == null || target.resolvedStyle.display == DisplayStyle.None ||
                target.worldBound.width <= 0 || target.worldBound.height <= 0)
            {
                Hide();
                return false;
            }

            Rect bounds = _root.WorldToLocal(target.worldBound);
            _outline.style.left = bounds.x - 3;
            _outline.style.top = bounds.y - 3;
            _outline.style.width = bounds.width + 6;
            _outline.style.height = bounds.height + 6;
            _outline.style.display = DisplayStyle.Flex;
            _outline.BringToFront();
            return true;
        }

        public void Hide() => _outline.style.display = DisplayStyle.None;
        public void Dispose() => _outline.RemoveFromHierarchy();
    }
}
