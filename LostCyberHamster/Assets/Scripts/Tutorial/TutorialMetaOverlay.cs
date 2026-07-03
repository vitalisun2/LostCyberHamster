using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialMetaOverlay
    {
        private const string _focusRootName = "tutorial-meta-focus-root";
        private const float _focusPadding = 18f;
        private const float _dimAlpha = 0.62f;
        private const float _softFocusWidth = 48f;
        private const int _focusMaskMaxWidth = 512;

        public static void ShowFocus(
            VisualElement documentRoot,
            VisualElement target,
            string instruction,
            TutorialFocusShape shape = TutorialFocusShape.Circle)
        {
            if (documentRoot == null || target == null)
            {
                return;
            }

            Hide(documentRoot);

            var root = new VisualElement { name = _focusRootName, pickingMode = PickingMode.Ignore };
            FillScreen(root);

            var mask = new VisualElement { name = "tutorial-meta-focus-mask", pickingMode = PickingMode.Ignore };
            FillScreen(mask);
            var highlight = new VisualElement { name = "tutorial-meta-focus-highlight", pickingMode = PickingMode.Ignore };
            highlight.style.position = Position.Absolute;
            highlight.style.backgroundColor = Color.clear;
            var bubble = CreateInstructionBubble(instruction);

            root.Add(mask);
            root.Add(highlight);
            root.Add(bubble);
            documentRoot.Add(root);

            root.schedule.Execute(() => ApplyFocus(documentRoot, target, mask, highlight, shape)).ExecuteLater(0);
            root.schedule.Execute(() => ApplyFocus(documentRoot, target, mask, highlight, shape)).ExecuteLater(100);
        }

        public static void Hide(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                return;
            }

            documentRoot.Q<VisualElement>(_focusRootName)?.RemoveFromHierarchy();
        }

        private static void ApplyFocus(
            VisualElement documentRoot,
            VisualElement target,
            VisualElement mask,
            VisualElement highlight,
            TutorialFocusShape shape)
        {
            Rect rootBounds = documentRoot.worldBound;
            if (rootBounds.width <= 0f || rootBounds.height <= 0f || target.worldBound.width <= 0f)
            {
                return;
            }

            Rect focusRect = GetTargetRect(documentRoot, target);
            Rect rootRect = new Rect(0f, 0f, rootBounds.width, rootBounds.height);
            var texture = TutorialFocusMaskBuilder.Create(
                focusRect,
                shape,
                rootRect,
                _dimAlpha,
                _softFocusWidth,
                _focusMaskMaxWidth);

            mask.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
            SetElementRect(highlight, focusRect);
            ApplyFocusRadius(highlight, focusRect, shape);
        }

        private static Rect GetTargetRect(VisualElement documentRoot, VisualElement target)
        {
            Rect rootBounds = documentRoot.worldBound;
            Rect targetBounds = target.worldBound;
            float padding = _focusPadding;
            return ClampRectToRoot(new Rect(
                targetBounds.x - rootBounds.x - padding,
                targetBounds.y - rootBounds.y - padding,
                targetBounds.width + padding * 2f,
                targetBounds.height + padding * 2f), rootBounds);
        }

        private static Rect ClampRectToRoot(Rect rect, Rect rootBounds)
        {
            float xMin = Mathf.Clamp(rect.xMin, 0f, rootBounds.width);
            float yMin = Mathf.Clamp(rect.yMin, 0f, rootBounds.height);
            float xMax = Mathf.Clamp(rect.xMax, xMin, rootBounds.width);
            float yMax = Mathf.Clamp(rect.yMax, yMin, rootBounds.height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static VisualElement CreateInstructionBubble(string instruction)
        {
            var bubble = new VisualElement { name = "tutorial-meta-instruction-bubble", pickingMode = PickingMode.Ignore };
            bubble.style.position = Position.Absolute;
            bubble.style.left = Length.Percent(50);
            bubble.style.top = Length.Percent(34);
            bubble.style.width = 720;
            bubble.style.minHeight = 112;
            bubble.style.marginLeft = -360;
            bubble.style.paddingTop = 20;
            bubble.style.paddingRight = 28;
            bubble.style.paddingBottom = 20;
            bubble.style.paddingLeft = 28;
            bubble.style.backgroundColor = new Color(0.98f, 0.92f, 0.45f, 0.96f);
            bubble.style.borderTopLeftRadius = 28;
            bubble.style.borderTopRightRadius = 28;
            bubble.style.borderBottomRightRadius = 28;
            bubble.style.borderBottomLeftRadius = 28;

            var label = CreateLabel("tutorial-meta-instruction", instruction, 38, TextAnchor.MiddleCenter);
            label.style.whiteSpace = WhiteSpace.Normal;
            bubble.Add(label);
            return bubble;
        }

        private static Label CreateLabel(string name, string text, int fontSize, TextAnchor align)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = align;
            label.style.unityTextOutlineWidth = 3;
            label.style.unityTextOutlineColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            return label;
        }

        private static void FillScreen(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
            element.style.left = 0;
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

        private static void ApplyFocusRadius(VisualElement element, Rect rect, TutorialFocusShape shape)
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
