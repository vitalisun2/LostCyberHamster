using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Разворачивает modal host и размещает рисованную композицию по эталону.
    /// </summary>
    internal sealed class GameResultModalPresentation
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;
        private static readonly ConditionalWeakTable<VisualElement, GameResultModalPresentation> _active = new();

        private readonly VisualElement _container;
        private readonly VisualElement _content;
        private readonly VisualElement _resultViewport;
        private readonly VisualElement _resultScaleFrame;
        private readonly VisualElement _resultDesign;
        private readonly Vector2 _referenceSize;
        private readonly ModalScaleMode _scaleMode;
        private readonly bool _useSafeArea;
        private readonly StyleEnum<Position> _framePosition;
        private readonly StyleLength _frameLeft;
        private readonly StyleLength _frameTop;
        private readonly StyleLength _frameWidth;
        private readonly StyleLength _frameHeight;
        private readonly StyleScale _designScale;
        private IVisualElementScheduledItem _layoutTask;
        private Rect _lastLayoutRect;
        private bool _hasLayout;

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
            VisualElement content,
            VisualElement viewport,
            VisualElement scaleFrame,
            VisualElement design,
            Vector2 referenceSize,
            ModalScaleMode scaleMode,
            bool useSafeArea)
        {
            _container = container;
            _content = content;
            _resultViewport = viewport;
            _resultScaleFrame = scaleFrame;
            _resultDesign = design;
            _referenceSize = referenceSize;
            _scaleMode = scaleMode;
            _useSafeArea = useSafeArea;
            if (scaleFrame != null)
            {
                _framePosition = scaleFrame.style.position;
                _frameLeft = scaleFrame.style.left;
                _frameTop = scaleFrame.style.top;
                _frameWidth = scaleFrame.style.width;
                _frameHeight = scaleFrame.style.height;
            }
            if (design != null)
                _designScale = design.style.scale;

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
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var content = root.Q<VisualElement>("modal__content");
            return ApplyCore(root,
                content?.Q<VisualElement>(className: "game-result-modal__viewport"),
                content?.Q<VisualElement>(className: "game-result-modal__scale-frame"),
                content?.Q<VisualElement>(className: "game-result-modal__design"),
                new Vector2(DesignWidth, DesignHeight), ModalScaleMode.Cover, false);
        }

        /// <summary>Размещает явную композицию; повторное применение сохраняет исходный host snapshot.</summary>
        public static GameResultModalPresentation Apply(
            VisualElement root,
            VisualElement viewport,
            VisualElement scaleFrame,
            VisualElement design,
            Vector2 referenceSize,
            ModalScaleMode scaleMode,
            bool useSafeArea)
        {
            if (root == null || viewport == null || scaleFrame == null || design == null)
                throw new ArgumentNullException(nameof(root), "Modal composition requires all layout elements.");
            if (!IsValidSize(referenceSize))
                throw new ArgumentOutOfRangeException(nameof(referenceSize));
            if (scaleMode != ModalScaleMode.Cover && scaleMode != ModalScaleMode.Contain)
                throw new ArgumentOutOfRangeException(nameof(scaleMode));

            return ApplyCore(root, viewport, scaleFrame, design, referenceSize, scaleMode, useSafeArea);
        }

        private static GameResultModalPresentation ApplyCore(
            VisualElement root, VisualElement viewport, VisualElement scaleFrame,
            VisualElement design, Vector2 referenceSize, ModalScaleMode scaleMode, bool useSafeArea)
        {
            var container = root.Q<VisualElement>("modal__container");
            var content = root.Q<VisualElement>("modal__content");
            if (container == null || content == null)
            {
                throw new MissingReferenceException(
                    "Game result modal host is missing required elements.");
            }

            // Один host хранит один исходный snapshot, даже при повторном Apply.
            if (_active.TryGetValue(container, out var previous))
            {
                if (previous._isApplied && previous._content == content &&
                    previous._resultViewport == viewport && previous._resultScaleFrame == scaleFrame &&
                    previous._resultDesign == design && previous._referenceSize == referenceSize &&
                    previous._scaleMode == scaleMode && previous._useSafeArea == useSafeArea)
                    return previous;
                previous.Restore();
            }

            var presentation = new GameResultModalPresentation(container, content, viewport,
                scaleFrame, design, referenceSize, scaleMode, useSafeArea);
            _active.Add(container, presentation);
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

            // Сначала отключаем отложенную работу, затем восстанавливаем оболочку.
            _isApplied = false;
            _layoutTask?.Pause();
            _layoutTask = null;
            _resultViewport?.UnregisterCallback<GeometryChangedEvent>(
                OnResultViewportGeometryChanged);

            // Возвращаем композицию в исходное состояние для смены режима на том же дереве.
            if (_resultScaleFrame != null)
            {
                _resultScaleFrame.style.position = _framePosition;
                _resultScaleFrame.style.left = _frameLeft;
                _resultScaleFrame.style.top = _frameTop;
                _resultScaleFrame.style.width = _frameWidth;
                _resultScaleFrame.style.height = _frameHeight;
            }
            if (_resultDesign != null)
                _resultDesign.style.scale = _designScale;

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
            if (_active.TryGetValue(_container, out var current) && ReferenceEquals(current, this))
                _active.Remove(_container);
        }

        private void ApplyFullscreenLayout()
        {
            _isApplied = true;
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
                ApplyResultLayout();
                _layoutTask = _resultViewport.schedule.Execute(ApplyResultLayout);
                if (_useSafeArea)
                    _layoutTask.Every(150);
            }
        }

        private void OnResultViewportGeometryChanged(
            GeometryChangedEvent evt)
        {
            ApplyResultLayout();
        }

        private void ApplyResultLayout()
        {
            if (!_isApplied || _resultViewport == null || _resultScaleFrame == null || _resultDesign == null)
            {
                return;
            }

            Rect available = _useSafeArea ? UiSafeArea.GetLocalRect(_resultViewport) : _resultViewport.contentRect;
            if (!IsValidSize(available.size) || (_hasLayout && available == _lastLayoutRect))
                return;
            _lastLayoutRect = available;
            _hasLayout = true;

            // Legacy cover совпадает с фоном; contain целиком удерживает новые панели.
            float widthScale = available.width / _referenceSize.x;
            float heightScale = available.height / _referenceSize.y;
            float scale = _scaleMode == ModalScaleMode.Cover
                ? Mathf.Max(widthScale, heightScale) : Mathf.Min(widthScale, heightScale);
            Vector2 frameSize = _referenceSize * scale;
            _resultScaleFrame.style.width = frameSize.x;
            _resultScaleFrame.style.height = frameSize.y;
            if (_useSafeArea)
            {
                _resultScaleFrame.style.position = Position.Absolute;
                _resultScaleFrame.style.left = available.center.x - frameSize.x * 0.5f;
                _resultScaleFrame.style.top = available.center.y - frameSize.y * 0.5f;
            }
            _resultDesign.style.scale = new Scale(
                new Vector3(scale, scale, 1f));
        }

        private static bool IsValidSize(Vector2 size)
        {
            return size.x > 0f && size.y > 0f &&
                !float.IsNaN(size.x) && !float.IsNaN(size.y) &&
                !float.IsInfinity(size.x) && !float.IsInfinity(size.y);
        }
    }
}
