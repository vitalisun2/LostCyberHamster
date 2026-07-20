using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Общая geometry и lifecycle focus-mask для gameplay и menu tutorial UI.
    /// </summary>
    public sealed class TutorialFocusOverlay : IDisposable
    {
        private readonly TutorialFocusMaskBuilder _maskBuilder = new();

        private VisualElement _mask;
        private VisualElement _highlight;
        private bool _isDisposed;

        public void Apply(
            VisualElement mask,
            VisualElement highlight,
            Rect focusRect,
            TutorialFocusShape shape,
            Rect rootRect,
            float dimAlpha,
            float softFocusWidth,
            int maxWidth)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TutorialFocusOverlay));
            }

            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            if (highlight == null)
            {
                throw new ArgumentNullException(nameof(highlight));
            }

            if (_mask != null && _mask != mask)
            {
                _mask.style.backgroundImage = null;
            }

            _mask = mask;
            _highlight = highlight;
            Texture2D texture = _maskBuilder.Build(
                focusRect,
                shape,
                rootRect,
                dimAlpha,
                softFocusWidth,
                maxWidth);
            _mask.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
            SetElementRect(_highlight, focusRect);
            ApplyFocusRadius(_highlight, focusRect, shape);
        }

        public void Clear()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_mask != null)
            {
                _mask.style.backgroundImage = null;
            }

            _mask = null;
            _highlight = null;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Clear();
            _maskBuilder.Dispose();
            _isDisposed = true;
        }

        public static Rect GetTargetRect(Rect rootBounds, Rect targetBounds, float padding)
        {
            var rect = new Rect(
                targetBounds.x - rootBounds.x - padding,
                targetBounds.y - rootBounds.y - padding,
                targetBounds.width + padding * 2f,
                targetBounds.height + padding * 2f);
            return ClampToRoot(rect, rootBounds.width, rootBounds.height);
        }

        public static Rect ClampToRoot(Rect rect, float rootWidth, float rootHeight)
        {
            float xMin = Mathf.Clamp(rect.xMin, 0f, rootWidth);
            float yMin = Mathf.Clamp(rect.yMin, 0f, rootHeight);
            float xMax = Mathf.Clamp(rect.xMax, xMin, rootWidth);
            float yMax = Mathf.Clamp(rect.yMax, yMin, rootHeight);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void SetElementRect(VisualElement element, Rect rect)
        {
            element.style.left = rect.x;
            element.style.top = rect.y;
            element.style.bottom = StyleKeyword.Auto;
            element.style.marginLeft = 0;
            element.style.width = rect.width;
            element.style.height = rect.height;
        }

        private static void ApplyFocusRadius(
            VisualElement element,
            Rect rect,
            TutorialFocusShape shape)
        {
            float radius = shape == TutorialFocusShape.Circle
                ? Mathf.Min(rect.width, rect.height) * 0.5f
                : 28f;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
        }
    }
}
