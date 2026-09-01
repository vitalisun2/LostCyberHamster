using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Разворачивает modal host и масштабирует игровые result-модалки по эталону.
    /// </summary>
    internal sealed class GameResultModalPresentation
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;

        private readonly VisualElement _container;
        private readonly VisualElement _content;
        private readonly VisualElement _resultViewport;
        private readonly VisualElement _resultScaleFrame;
        private readonly VisualElement _resultDesign;

        private readonly StyleLength _containerWidth;
        private readonly StyleLength _containerHeight;
        private readonly StyleLength _containerMinWidth;
        private readonly StyleLength _containerMinHeight;
        private readonly StyleBackground _containerBackgroundImage;
        private readonly StyleColor _containerBackgroundColor;
        private readonly StyleFloat _borderTopWidth;
        private readonly StyleFloat _borderRightWidth;
        private readonly StyleFloat _borderBottomWidth;
        private readonly StyleFloat _borderLeftWidth;
        private readonly StyleLength _borderTopLeftRadius;
        private readonly StyleLength _borderTopRightRadius;
        private readonly StyleLength _borderBottomRightRadius;
        private readonly StyleLength _borderBottomLeftRadius;
        private readonly StyleEnum<Overflow> _containerOverflow;

        private readonly StyleLength _contentWidth;
        private readonly StyleLength _contentHeight;
        private readonly StyleLength _contentMarginTop;
        private readonly StyleLength _contentMarginRight;
        private readonly StyleLength _contentMarginBottom;
        private readonly StyleLength _contentMarginLeft;
        private readonly StyleEnum<Align> _contentAlignSelf;
        private readonly StyleEnum<Justify> _contentJustifyContent;

        private bool _isApplied;

        private GameResultModalPresentation(
            VisualElement container,
            VisualElement content)
        {
            _container = container;
            _content = content;
            _resultViewport = content.Q<VisualElement>(
                className: "game-result-modal__viewport");
            _resultScaleFrame = content.Q<VisualElement>(
                className: "game-result-modal__scale-frame");
            _resultDesign = content.Q<VisualElement>(
                className: "game-result-modal__design");

            _containerWidth = container.style.width;
            _containerHeight = container.style.height;
            _containerMinWidth = container.style.minWidth;
            _containerMinHeight = container.style.minHeight;
            _containerBackgroundImage = container.style.backgroundImage;
            _containerBackgroundColor = container.style.backgroundColor;
            _borderTopWidth = container.style.borderTopWidth;
            _borderRightWidth = container.style.borderRightWidth;
            _borderBottomWidth = container.style.borderBottomWidth;
            _borderLeftWidth = container.style.borderLeftWidth;
            _borderTopLeftRadius = container.style.borderTopLeftRadius;
            _borderTopRightRadius = container.style.borderTopRightRadius;
            _borderBottomRightRadius = container.style.borderBottomRightRadius;
            _borderBottomLeftRadius = container.style.borderBottomLeftRadius;
            _containerOverflow = container.style.overflow;

            _contentWidth = content.style.width;
            _contentHeight = content.style.height;
            _contentMarginTop = content.style.marginTop;
            _contentMarginRight = content.style.marginRight;
            _contentMarginBottom = content.style.marginBottom;
            _contentMarginLeft = content.style.marginLeft;
            _contentAlignSelf = content.style.alignSelf;
            _contentJustifyContent = content.style.justifyContent;
        }

        /// <summary>
        /// Применяет полноэкранный host и reference-scale result-модалки.
        /// </summary>
        public static GameResultModalPresentation Apply(VisualElement root)
        {
            var container = root.Q<VisualElement>("modal__container");
            var content = root.Q<VisualElement>("modal__content");
            if (container == null || content == null)
            {
                throw new MissingReferenceException(
                    "Game result modal host is missing required elements.");
            }

            var presentation = new GameResultModalPresentation(
                container,
                content);
            presentation.ApplyFullscreenLayout();
            return presentation;
        }

        /// <summary>
        /// Восстанавливает layout общего modal host после закрытия result-модалки.
        /// </summary>
        public void Restore()
        {
            if (!_isApplied)
            {
                return;
            }

            _resultViewport?.UnregisterCallback<GeometryChangedEvent>(
                OnResultViewportGeometryChanged);

            _container.style.width = _containerWidth;
            _container.style.height = _containerHeight;
            _container.style.minWidth = _containerMinWidth;
            _container.style.minHeight = _containerMinHeight;
            _container.style.backgroundImage = _containerBackgroundImage;
            _container.style.backgroundColor = _containerBackgroundColor;
            _container.style.borderTopWidth = _borderTopWidth;
            _container.style.borderRightWidth = _borderRightWidth;
            _container.style.borderBottomWidth = _borderBottomWidth;
            _container.style.borderLeftWidth = _borderLeftWidth;
            _container.style.borderTopLeftRadius = _borderTopLeftRadius;
            _container.style.borderTopRightRadius = _borderTopRightRadius;
            _container.style.borderBottomRightRadius = _borderBottomRightRadius;
            _container.style.borderBottomLeftRadius = _borderBottomLeftRadius;
            _container.style.overflow = _containerOverflow;

            _content.style.width = _contentWidth;
            _content.style.height = _contentHeight;
            _content.style.marginTop = _contentMarginTop;
            _content.style.marginRight = _contentMarginRight;
            _content.style.marginBottom = _contentMarginBottom;
            _content.style.marginLeft = _contentMarginLeft;
            _content.style.alignSelf = _contentAlignSelf;
            _content.style.justifyContent = _contentJustifyContent;
            _isApplied = false;
        }

        private void ApplyFullscreenLayout()
        {
            _container.style.width = Length.Percent(100f);
            _container.style.height = Length.Percent(100f);
            _container.style.minWidth = 0f;
            _container.style.minHeight = 0f;
            _container.style.backgroundImage = StyleKeyword.None;
            _container.style.backgroundColor = Color.clear;
            _container.style.borderTopWidth = 0f;
            _container.style.borderRightWidth = 0f;
            _container.style.borderBottomWidth = 0f;
            _container.style.borderLeftWidth = 0f;
            _container.style.borderTopLeftRadius = 0f;
            _container.style.borderTopRightRadius = 0f;
            _container.style.borderBottomRightRadius = 0f;
            _container.style.borderBottomLeftRadius = 0f;
            _container.style.overflow = Overflow.Hidden;

            _content.style.width = Length.Percent(100f);
            _content.style.height = Length.Percent(100f);
            _content.style.marginTop = 0f;
            _content.style.marginRight = 0f;
            _content.style.marginBottom = 0f;
            _content.style.marginLeft = 0f;
            _content.style.alignSelf = Align.Stretch;
            _content.style.justifyContent = Justify.FlexStart;

            if (_resultViewport != null &&
                _resultScaleFrame != null &&
                _resultDesign != null)
            {
                _resultViewport.RegisterCallback<GeometryChangedEvent>(
                    OnResultViewportGeometryChanged);
                ApplyResultLayout(_resultViewport.contentRect.size);
                _resultViewport.schedule.Execute(() =>
                {
                    if (_isApplied)
                    {
                        ApplyResultLayout(
                            _resultViewport.contentRect.size);
                    }
                });
            }

            _isApplied = true;
        }

        private void OnResultViewportGeometryChanged(
            GeometryChangedEvent evt)
        {
            ApplyResultLayout(evt.newRect.size);
        }

        private void ApplyResultLayout(Vector2 viewportSize)
        {
            if (_resultScaleFrame == null || _resultDesign == null)
            {
                return;
            }

            // Повторяем cover-масштаб фонового PNG для точного совмещения.
            float width = Mathf.Max(1f, viewportSize.x);
            float height = Mathf.Max(1f, viewportSize.y);
            float scale = Mathf.Max(
                width / DesignWidth,
                height / DesignHeight);

            // Frame центрирует обрезаемый эталон, design масштабирует весь контент.
            _resultScaleFrame.style.width = DesignWidth * scale;
            _resultScaleFrame.style.height = DesignHeight * scale;
            _resultDesign.style.scale = new Scale(
                new Vector3(scale, scale, 1f));
        }
    }
}
