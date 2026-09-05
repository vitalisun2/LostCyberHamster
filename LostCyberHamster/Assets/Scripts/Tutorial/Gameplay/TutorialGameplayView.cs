using System;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Отображает gameplay-подсказки tutorial и передаёт UI-ввод наружу.
    /// </summary>
    public sealed class TutorialGameplayView : IDisposable
    {
        private const string _rootName = "tutorial-root";
        private const string _completeRootName = "tutorial-complete-root";
        private const float _focusPadding = 18f;
        private const float _fingerSize = 56f;
        private const float _instructionWidth = 228f;
        private const float _instructionHeight = 88f;
        private const float _dimAlpha = 0.62f;
        private const float _softFocusWidth = 48f;
        private const int _focusMaskMaxWidth = 512;

        private readonly VisualElement _root;
        private readonly VisualElement _tapTarget;
        private readonly VisualElement _jumpTarget;
        private readonly VisualElement _idleInputBlocker;
        private readonly VisualElement _headerRoot;
        private readonly VisualElement _promptRoot;
        private readonly VisualElement _completeRoot;
        private readonly VisualElement _focusMask;
        private readonly VisualElement _focusHighlight;
        private readonly VisualElement _promptInputCapture;
        private readonly VisualElement _finger;
        private readonly VisualElement _instructionBubble;
        private readonly Label _titleLabel;
        private readonly Label _lessonLabel;
        private readonly Label _lessonNumberLabel;
        private readonly Label _instructionLabel;
        private readonly Label _completeTitleLabel;
        private readonly Label _completeMessageLabel;
        private readonly Button _skipButton;
        private readonly Button _primaryCompletionButton;
        private readonly Button _secondaryCompletionButton;
        private readonly TutorialFocusOverlay _focusOverlay = new();
        private readonly IVisualElementScheduledItem _layoutTask;

        private IVisualElementScheduledItem _focusRefreshTask;
        private TutorialAction _currentPromptAction;
        private TutorialFocusShape _currentFocusShape;
        private Rect _currentFocusRect;
        private Rect _currentFocusRootRect;
        private Rect _lastRootBounds;
        private Rect _lastTapBounds;
        private Rect _lastJumpBounds;
        private Rect _safeRect;
        private float _artScale = 1f;
        private bool _hasLayoutSnapshot;
        private bool _hasCurrentFocusRect;
        private bool _isPromptVisible;
        private int _focusVersion;
        private bool _isDisposed;

        /// <summary>Создаёт слои подсказок поверх существующих gameplay-кнопок.</summary>
        public TutorialGameplayView(VisualElement contentRoot)
        {
            if (contentRoot == null)
            {
                throw new ArgumentNullException(nameof(contentRoot));
            }

            // Находит фактические зоны ввода и заменяет прежнее представление.
            _tapTarget = contentRoot.Q<VisualElement>("tap");
            _jumpTarget = contentRoot.Q<VisualElement>("btn_jump");
            RemoveExistingTutorialLayers(contentRoot);
            _root = CreateDecoration(_rootName, "tutorial-fill");
            _headerRoot = CreateHeader(out _titleLabel, out _lessonLabel, out _lessonNumberLabel);
            _skipButton = new Button { name = "tutorial-skip", pickingMode = PickingMode.Position };
            _idleInputBlocker = CreateInputLayer("tutorial-idle-input-blocker");
            _promptRoot = CreateDecoration("tutorial-prompt-root", "tutorial-fill");
            _focusMask = CreateDecoration("tutorial-focus-mask", "tutorial-fill");
            _focusHighlight = CreateDecoration("tutorial-focus-highlight");
            _promptInputCapture = CreateInputLayer("tutorial-prompt-input-capture");
            _finger = CreateDecoration("tutorial-finger");
            _instructionBubble = CreateDecoration("tutorial-instruction-bubble");
            _instructionLabel = CreateLabel("tutorial-instruction");
            _instructionBubble.Add(_instructionLabel);
            _completeRoot = CreateCompletionRoot(
                out _completeTitleLabel,
                out _completeMessageLabel,
                out _primaryCompletionButton,
                out _secondaryCompletionButton);

            // Capture закрывает gameplay, художественные элементы пропускают picking.
            _promptRoot.Add(_focusMask);
            _promptRoot.Add(_focusHighlight);
            _promptRoot.Add(_finger);
            _promptRoot.Add(_instructionBubble);
            _promptRoot.Add(_promptInputCapture);
            _root.Add(_idleInputBlocker);
            _root.Add(_promptRoot);
            _root.Add(_headerRoot);
            _root.Add(_skipButton);
            contentRoot.Add(_root);
            contentRoot.Add(_completeRoot);

            // Geometry callbacks дополняются проверкой safe area и сдвигов предков.
            RegisterCallbacks();
            _layoutTask = _root.schedule.Execute(RefreshGeometry).Every(100);
            Hide();
        }

        public event Action<TutorialAction> GameplayActionRequested;
        public event Action SkipRequested;
        public event Action PrimaryCompletionRequested;
        public event Action SecondaryCompletionRequested;

        /// <summary>Показывает локализованный урок и его номер во время подхода к препятствию.</summary>
        public void ShowHeader(string titleKey, int number)
        {
            // Текст остаётся нативным и отделён от художественных плашек.
            _titleLabel.text = Localize("tutorial_title");
            _lessonLabel.text = Localize(titleKey);
            _lessonNumberLabel.text = number.ToString();
            _skipButton.text = Localize("btn_skip");

            // Между препятствиями tutorial удерживает игровой ввод.
            _completeRoot.style.display = DisplayStyle.None;
            _root.style.display = DisplayStyle.Flex;
            _idleInputBlocker.style.display = DisplayStyle.Flex;
            HidePrompt();
            _hasLayoutSnapshot = false;
            _layoutTask.Resume();
            RefreshGeometry();
        }

        /// <summary>Показывает инструкцию и открывает ввод внутри подсвеченной формы.</summary>
        public void ShowPrompt(string instructionKey, TutorialAction focusAction)
        {
            // Jump и SuperJump сохраняют непрерывный фокус одной кнопки.
            _instructionLabel.text = Localize(instructionKey);
            bool keepFocusRect = _hasCurrentFocusRect
                                 && UsesJumpTarget(_currentPromptAction) == UsesJumpTarget(focusAction);
            _currentPromptAction = focusAction;
            _isPromptVisible = true;
            _root.style.display = DisplayStyle.Flex;
            _idleInputBlocker.style.display = DisplayStyle.None;
            _promptRoot.style.display = DisplayStyle.Flex;
            if (!keepFocusRect)
            {
                _hasCurrentFocusRect = false;
            }

            // Отложенный проход учитывает layout после показа слоя.
            _layoutTask.Resume();
            QueueFocusRefresh();
        }

        /// <summary>Скрывает текущую подсказку и отменяет её отложенное обновление.</summary>
        public void HidePrompt()
        {
            _isPromptVisible = false;
            _promptRoot.style.display = DisplayStyle.None;
            CancelFocusRefresh();
            _hasCurrentFocusRect = false;
            _focusOverlay.Clear();
        }

        /// <summary>Показывает существующее окно завершения с заданными двумя действиями.</summary>
        public void ShowCompletion(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText,
            bool showPrimaryButton)
        {
            // Завершение использует прежний контракт orchestrator.
            Hide();
            _completeTitleLabel.text = title ?? string.Empty;
            _completeMessageLabel.text = message ?? string.Empty;
            _primaryCompletionButton.text = primaryButtonText ?? string.Empty;
            _secondaryCompletionButton.text = secondaryButtonText ?? string.Empty;

            // Внешний сценарий определяет доступность основного действия.
            _primaryCompletionButton.style.display = showPrimaryButton
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _completeRoot.style.display = DisplayStyle.Flex;
        }

        /// <summary>Скрывает оба слоя и останавливает обновление скрытого HUD.</summary>
        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _completeRoot.style.display = DisplayStyle.None;
            _idleInputBlocker.style.display = DisplayStyle.None;
            HidePrompt();
            _layoutTask.Pause();
        }

        /// <summary>Отменяет callbacks, schedule и принадлежащую представлению маску.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            // Останавливает отложенные операции до удаления элементов.
            _isDisposed = true;
            _layoutTask.Pause();
            CancelFocusRefresh();
            UnregisterCallbacks();
            _focusOverlay.Dispose();

            // Освобождает UI и подписчиков владельца.
            _root.RemoveFromHierarchy();
            _completeRoot.RemoveFromHierarchy();
            GameplayActionRequested = null;
            SkipRequested = null;
            PrimaryCompletionRequested = null;
            SecondaryCompletionRequested = null;
        }

        /// <summary>Подписывает действия и изменения геометрии обоих targets.</summary>
        private void RegisterCallbacks()
        {
            _skipButton.RegisterCallback<ClickEvent>(HandleSkipClicked);
            _primaryCompletionButton.RegisterCallback<ClickEvent>(HandlePrimaryCompletionClicked);
            _secondaryCompletionButton.RegisterCallback<ClickEvent>(HandleSecondaryCompletionClicked);
            _promptInputCapture.RegisterCallback<PointerDownEvent>(HandlePromptPointerDown);
            _root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            _tapTarget?.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            _jumpTarget?.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
        }

        /// <summary>Снимает все подписки, включая внешние gameplay-targets.</summary>
        private void UnregisterCallbacks()
        {
            _skipButton.UnregisterCallback<ClickEvent>(HandleSkipClicked);
            _primaryCompletionButton.UnregisterCallback<ClickEvent>(HandlePrimaryCompletionClicked);
            _secondaryCompletionButton.UnregisterCallback<ClickEvent>(HandleSecondaryCompletionClicked);
            _promptInputCapture.UnregisterCallback<PointerDownEvent>(HandlePromptPointerDown);
            _root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            _tapTarget?.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            _jumpTarget?.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
        }

        private void HandleSkipClicked(ClickEvent evt)
        {
            SkipRequested?.Invoke();
            evt.StopImmediatePropagation();
        }

        private void HandlePrimaryCompletionClicked(ClickEvent evt)
        {
            PrimaryCompletionRequested?.Invoke();
            evt.StopImmediatePropagation();
        }

        private void HandleSecondaryCompletionClicked(ClickEvent evt)
        {
            SecondaryCompletionRequested?.Invoke();
            evt.StopImmediatePropagation();
        }

        /// <summary>Проверяет ту же форму, из которой построена видимая маска.</summary>
        private void HandlePromptPointerDown(PointerDownEvent evt)
        {
            Vector2 point = _root.WorldToLocal(evt.position);
            if (_hasCurrentFocusRect
                && TutorialFocusMaskBuilder.ContainsFocusPoint(
                    point, _currentFocusRect, _currentFocusRootRect, _currentFocusShape))
            {
                GameplayActionRequested?.Invoke(_currentPromptAction);
            }

            evt.StopImmediatePropagation();
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshGeometry();
        }

        /// <summary>Обновляет художественную safe area и замечает сдвиги targets через предков.</summary>
        private void RefreshGeometry()
        {
            if (_isDisposed || _root.panel == null || _root.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            // Сравнивает реальные panel bounds, включая изменения родительских transforms.
            Rect rootBounds = _root.worldBound;
            Rect tapBounds = _tapTarget?.worldBound ?? default;
            Rect jumpBounds = _jumpTarget?.worldBound ?? default;
            Rect safe = UiSafeArea.GetLocalRect(_root);
            if (safe.width <= 0f || safe.height <= 0f
                || (_hasLayoutSnapshot && rootBounds == _lastRootBounds
                    && tapBounds == _lastTapBounds && jumpBounds == _lastJumpBounds && safe == _safeRect))
            {
                return;
            }

            // Масштабирует только графику; маска и ввод остаются в локальных единицах root.
            _hasLayoutSnapshot = true;
            _lastRootBounds = rootBounds;
            _lastTapBounds = tapBounds;
            _lastJumpBounds = jumpBounds;
            _safeRect = safe;
            _artScale = Mathf.Min(safe.width / 900f, safe.height / 450f);
            SetArtPosition(_headerRoot, safe.center.x - 100f * _artScale, safe.yMin + 12f * _artScale);
            SetArtPosition(_skipButton, safe.center.x - 68f * _artScale, safe.yMax - 74f * _artScale);
            if (_isPromptVisible)
            {
                QueueFocusRefresh();
            }
        }

        /// <summary>Объединяет изменения layout в один отменяемый проход фокуса.</summary>
        private void QueueFocusRefresh()
        {
            CancelFocusRefresh();
            int focusVersion = _focusVersion;
            _focusRefreshTask = _promptRoot.schedule.Execute(() =>
            {
                _focusRefreshTask = null;
                ApplyFocusStyle(focusVersion);
            });
        }

        /// <summary>Отменяет устаревший schedule и инвалидирует его callback.</summary>
        private void CancelFocusRefresh()
        {
            _focusVersion++;
            _focusRefreshTask?.Pause();
            _focusRefreshTask = null;
        }

        private static bool UsesJumpTarget(TutorialAction action)
        {
            return action == TutorialAction.Jump || action == TutorialAction.SuperJump;
        }

        /// <summary>Строит фокус по фактическому target в координатах немасштабированного root.</summary>
        private void ApplyFocusStyle(int focusVersion)
        {
            if (_isDisposed || !_isPromptVisible || focusVersion != _focusVersion || _root.panel == null)
            {
                return;
            }

            // Первое раскрытие ждёт безопасную геометрию художественного слоя.
            if (!_hasLayoutSnapshot)
            {
                RefreshGeometry();
                return;
            }

            // При отсутствии layout ввод остаётся закрытым до geometry callback.
            bool jump = UsesJumpTarget(_currentPromptAction);
            VisualElement target = jump ? _jumpTarget : _tapTarget;
            Rect rootRect = _root.contentRect;
            if (target == null || target.panel != _root.panel || !target.visible
                || target.resolvedStyle.display == DisplayStyle.None
                || target.worldBound.width <= 0f || target.worldBound.height <= 0f
                || rootRect.width <= 0f || rootRect.height <= 0f)
            {
                _hasCurrentFocusRect = false;
                _focusOverlay.Clear();
                return;
            }

            // Одни rect и shape владеют и маской, и проверкой PointerDown.
            Rect bounds = target.worldBound;
            Vector2 min = _root.WorldToLocal(bounds.min);
            Vector2 max = _root.WorldToLocal(bounds.max);
            float padding = jump ? _focusPadding : 0f;
            _currentFocusRect = TutorialFocusOverlay.ClampToRoot(
                Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding),
                rootRect.width, rootRect.height);
            _currentFocusRootRect = rootRect;
            _currentFocusShape = jump ? TutorialFocusShape.Circle : TutorialFocusShape.RoundedRect;
            _hasCurrentFocusRect = _currentFocusRect.width > 0f && _currentFocusRect.height > 0f;
            _focusOverlay.Apply(
                _focusMask, _focusHighlight, _currentFocusRect, _currentFocusShape, rootRect,
                _dimAlpha, _softFocusWidth, _focusMaskMaxWidth);
            PositionPromptArt(_currentFocusRect, !jump);
        }

        /// <summary>Размещает инструкцию и руку рядом с фокусом внутри художественной safe area.</summary>
        private void PositionPromptArt(Rect focusRect, bool showFinger)
        {
            // Инструкция сохраняет отдельный запас между header и Skip.
            float width = _instructionWidth * _artScale;
            float height = _instructionHeight * _artScale;
            float desiredX = showFinger
                ? focusRect.center.x + focusRect.width * 0.2f
                : focusRect.center.x - width * 0.5f;
            float left = Mathf.Clamp(desiredX, _safeRect.xMin + 8f * _artScale,
                _safeRect.xMax - width - 8f * _artScale);
            float top = Mathf.Clamp(focusRect.yMin - height - 16f * _artScale,
                _safeRect.yMin + 140f * _artScale, _safeRect.yMax - 172f * _artScale);
            SetArtPosition(_instructionBubble, left, top);

            // Рука является декорацией и не меняет принимаемую область ввода.
            _finger.style.display = showFinger ? DisplayStyle.Flex : DisplayStyle.None;
            if (showFinger)
            {
                float size = _fingerSize * _artScale;
                float fingerLeft = Mathf.Clamp(focusRect.center.x - size * 0.5f,
                    _safeRect.xMin, _safeRect.xMax - size);
                float fingerTop = Mathf.Clamp(focusRect.y + focusRect.height * 0.55f - size * 0.5f,
                    _safeRect.yMin, _safeRect.yMax - size);
                SetArtPosition(_finger, fingerLeft, fingerTop);
            }
        }

        /// <summary>Применяет динамический масштаб и позицию отдельного художественного элемента.</summary>
        private void SetArtPosition(VisualElement element, float left, float top)
        {
            element.style.left = left;
            element.style.top = top;
            element.style.scale = new Scale(new Vector3(_artScale, _artScale, 1f));
        }

        /// <summary>Создаёт заголовок и отдельные lesson-плашку, badge и нативные подписи.</summary>
        private static VisualElement CreateHeader(out Label title, out Label lesson, out Label number)
        {
            var header = CreateDecoration("tutorial-header");
            title = CreateLabel("tutorial-title");
            var panel = CreateDecoration("tutorial-lesson-panel");
            var badge = CreateDecoration("tutorial-lesson-badge");
            lesson = CreateLabel("tutorial-lesson-title");
            number = CreateLabel("tutorial-lesson-number");
            badge.Add(number);
            panel.Add(lesson);
            panel.Add(badge);
            header.Add(title);
            header.Add(panel);
            return header;
        }

        /// <summary>Создаёт прежнее представление завершения с двумя кнопками.</summary>
        private static VisualElement CreateCompletionRoot(
            out Label titleLabel,
            out Label messageLabel,
            out Button primaryButton,
            out Button secondaryButton)
        {
            var root = CreateInputLayer(_completeRootName);
            root.AddToClassList("tutorial-overlay");
            var container = CreateDecoration("tutorial-complete-container", "tutorial-surface");
            titleLabel = CreateLabel("tutorial-complete-title", "tutorial-text");
            messageLabel = CreateLabel("tutorial-complete-message", "tutorial-text");
            var buttons = CreateDecoration("tutorial-complete-buttons");
            primaryButton = CreateCompletionButton("tutorial-complete-play", "Играть");
            secondaryButton = CreateCompletionButton("tutorial-complete-menu", "Меню");
            buttons.Add(primaryButton);
            buttons.Add(secondaryButton);
            container.Add(titleLabel);
            container.Add(messageLabel);
            container.Add(buttons);
            root.Add(container);
            return root;
        }

        private static Button CreateCompletionButton(string name, string text)
        {
            var button = new Button { name = name, text = text, pickingMode = PickingMode.Position };
            button.AddToClassList("tutorial-button");
            return button;
        }

        private static VisualElement CreateInputLayer(string name)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Position };
            element.AddToClassList("tutorial-fill");
            return element;
        }

        private static VisualElement CreateDecoration(string name, string className = null)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            if (className != null)
            {
                element.AddToClassList(className);
            }

            return element;
        }

        private static Label CreateLabel(string name, string className = null)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            if (className != null)
            {
                label.AddToClassList(className);
            }

            return label;
        }

        private static string Localize(string key)
        {
            string text = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(text) ? key ?? string.Empty : text;
        }

        private static void RemoveExistingTutorialLayers(VisualElement contentRoot)
        {
            contentRoot.Q<VisualElement>(_rootName)?.RemoveFromHierarchy();
            contentRoot.Q<VisualElement>(_completeRootName)?.RemoveFromHierarchy();
        }
    }
}
