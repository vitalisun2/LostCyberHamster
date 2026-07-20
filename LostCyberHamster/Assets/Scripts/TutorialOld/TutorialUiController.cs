using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.TutorialOld
{
    /// <summary>
    /// Управляет tutorial-слоем внутри игрового экрана.
    /// </summary>
    public sealed class TutorialUiController
    {
        private const string _fingerResourcePath = "Tutorial/tutorial_finger_placeholder";
        private const string _rootName = "tutorial-root";
        private const string _completeRootName = "tutorial-complete-root";
        private const float _focusPadding = 18f;
        private const float _fingerSize = 160f;
        private const float _dimAlpha = 0.62f;
        private const float _softFocusWidth = 48f;
        private const int _focusMaskMaxWidth = 512;

        private readonly VisualElement _root;
        private readonly VisualElement _tapTarget;
        private readonly VisualElement _jumpTarget;
        private readonly VisualElement _ultraTarget;
        private readonly VisualElement _idleInputBlocker;
        private readonly VisualElement _promptRoot;
        private readonly VisualElement _completeRoot;
        private readonly VisualElement _focusMask;
        private readonly VisualElement _focusHighlight;
        private readonly VisualElement _promptInputCapture;
        private readonly VisualElement _finger;
        private readonly Label _titleLabel;
        private readonly Label _instructionLabel;
        private readonly Label _completeTitleLabel;
        private readonly Label _completeMessageLabel;
        private readonly Button _skipButton;
        private readonly Button _completePlayButton;
        private readonly Button _completeMenuButton;

        private Action _skipAction;
        private Action _completePlayAction;
        private Action _completeMenuAction;
        private Action<TutorialAction> _gameplayAction;
        private Texture2D _focusMaskTexture;
        private TutorialAction _currentPromptAction;
        private Rect _currentFocusRect;
        private bool _hasCurrentFocusRect;

        public TutorialUiController(
            VisualElement contentRoot,
            VisualElement tapTarget,
            VisualElement jumpTarget,
            VisualElement ultraTarget)
        {
            RemoveExistingTutorialLayers(contentRoot);
            _root = CreateRoot();
            _tapTarget = tapTarget;
            _jumpTarget = jumpTarget;
            _ultraTarget = ultraTarget;
            _titleLabel = CreateTitle();
            _skipButton = CreateSkipButton();
            _idleInputBlocker = CreateIdleInputBlocker();
            _promptRoot = CreatePromptRoot(
                out _focusMask,
                out _focusHighlight,
                out _promptInputCapture,
                out _finger,
                out _instructionLabel);
            _completeRoot = CreateCompleteRoot(
                out _completeTitleLabel,
                out _completeMessageLabel,
                out _completePlayButton,
                out _completeMenuButton);

            _root.Add(_idleInputBlocker);
            _root.Add(_promptRoot);
            _root.Add(_titleLabel);
            _root.Add(_skipButton);
            contentRoot.Add(_root);
            contentRoot.Add(_completeRoot);

            Hide();
        }

        /// <summary>
        /// Назначает действия кнопок tutorial UI.
        /// </summary>
        public void SetActions(Action skipAction, Action playAction, Action menuAction)
        {
            _skipAction = skipAction;
            _completePlayAction = playAction;
            _completeMenuAction = menuAction;
        }

        public void SetGameplayAction(Action<TutorialAction> gameplayAction)
        {
            _gameplayAction = gameplayAction;
        }

        /// <summary>
        /// Подписывает кнопки tutorial UI на действия.
        /// </summary>
        public void SubscribeToEvents()
        {
            _skipButton.RegisterCallback<ClickEvent>(OnClickSkip);
            _completePlayButton.RegisterCallback<ClickEvent>(OnClickCompletePlay);
            _completeMenuButton.RegisterCallback<ClickEvent>(OnClickCompleteMenu);
            _promptInputCapture.RegisterCallback<PointerDownEvent>(OnPromptPointerDown);
        }

        /// <summary>
        /// Отписывает кнопки tutorial UI от действий.
        /// </summary>
        public void UnsubscribeFromEvents()
        {
            _skipButton.UnregisterCallback<ClickEvent>(OnClickSkip);
            _completePlayButton.UnregisterCallback<ClickEvent>(OnClickCompletePlay);
            _completeMenuButton.UnregisterCallback<ClickEvent>(OnClickCompleteMenu);
            _promptInputCapture.UnregisterCallback<PointerDownEvent>(OnPromptPointerDown);
        }

        /// <summary>
        /// Показывает постоянный заголовок текущего урока.
        /// </summary>
        public void ShowHeader(string title)
        {
            _titleLabel.text = title;
            _root.style.display = DisplayStyle.Flex;
            _idleInputBlocker.style.display = DisplayStyle.Flex;
            _skipButton.style.display = DisplayStyle.Flex;
            HidePrompt();
        }

        /// <summary>
        /// Показывает затемнение, подсветку tap-области и инструкцию.
        /// </summary>
        public void ShowPrompt(string instruction)
        {
            ShowPrompt(instruction, TutorialAction.Tap);
        }

        /// <summary>
        /// Показывает затемнение, подсветку области действия и инструкцию.
        /// </summary>
        public void ShowPrompt(string instruction, TutorialAction focusAction)
        {
            _instructionLabel.text = instruction;
            _currentPromptAction = focusAction;
            _root.style.display = DisplayStyle.Flex;
            _idleInputBlocker.style.display = DisplayStyle.None;
            _promptRoot.style.display = DisplayStyle.Flex;
            ApplyFocusStyle(focusAction);
            _promptRoot.schedule.Execute(() => ApplyFocusStyle(focusAction)).ExecuteLater(0);
        }

        /// <summary>
        /// Скрывает подсказку действия, оставляя заголовок и skip.
        /// </summary>
        public void HidePrompt()
        {
            _promptRoot.style.display = DisplayStyle.None;
            _hasCurrentFocusRect = false;
            ClearFocusMaskTexture();
        }

        /// <summary>
        /// Показывает финальное окно прохождения tutorial.
        /// </summary>
        public void ShowComplete(string title, string message)
        {
            ShowComplete(title, message, "Играть", "Меню", true);
        }

        public void ShowComplete(
            string title,
            string message,
            string playButtonText,
            string menuButtonText,
            bool showPlayButton)
        {
            Hide();
            _completeTitleLabel.text = title;
            _completeMessageLabel.text = message;
            _completePlayButton.text = playButtonText;
            _completeMenuButton.text = menuButtonText;
            _completePlayButton.style.display = showPlayButton ? DisplayStyle.Flex : DisplayStyle.None;
            _completeRoot.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Скрывает все tutorial-слои.
        /// </summary>
        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _completeRoot.style.display = DisplayStyle.None;
            _idleInputBlocker.style.display = DisplayStyle.None;
            ClearFocusMaskTexture();
        }

        private void OnClickSkip(ClickEvent evt)
        {
            _skipAction?.Invoke();
        }

        private void OnClickCompletePlay(ClickEvent evt)
        {
            _completePlayAction?.Invoke();
        }

        private void OnClickCompleteMenu(ClickEvent evt)
        {
            _completeMenuAction?.Invoke();
        }

        private void OnPromptPointerDown(PointerDownEvent evt)
        {
            if (_hasCurrentFocusRect && _currentFocusRect.Contains(evt.localPosition))
            {
                _gameplayAction?.Invoke(_currentPromptAction);
            }

            evt.StopImmediatePropagation();
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement { name = _rootName, pickingMode = PickingMode.Ignore };
            FillScreen(root);
            return root;
        }

        private static Label CreateTitle()
        {
            var title = CreateLabel("tutorial-title", 44, TextAnchor.MiddleCenter);
            title.style.position = Position.Absolute;
            title.style.top = 18;
            title.style.left = 0;
            title.style.width = Length.Percent(100);
            title.style.height = 76;
            return title;
        }

        private static Button CreateSkipButton()
        {
            var button = CreateButton("tutorial-skip", "Пропустить");
            button.pickingMode = PickingMode.Position;
            button.style.position = Position.Absolute;
            button.style.bottom = 24;
            button.style.left = Length.Percent(50);
            button.style.marginLeft = -150;
            button.style.width = 300;
            button.style.height = 82;
            return button;
        }

        private static VisualElement CreateIdleInputBlocker()
        {
            var blocker = new VisualElement
            {
                name = "tutorial-idle-input-blocker",
                pickingMode = PickingMode.Position
            };
            FillScreen(blocker);
            blocker.style.backgroundColor = Color.clear;
            return blocker;
        }

        private static VisualElement CreatePromptRoot(
            out VisualElement focusMask,
            out VisualElement focusHighlight,
            out VisualElement promptInputCapture,
            out VisualElement finger,
            out Label instructionLabel)
        {
            var root = new VisualElement { name = "tutorial-prompt-root", pickingMode = PickingMode.Ignore };
            FillScreen(root);

            focusMask = CreateFocusMask();
            focusHighlight = CreateFocusHighlight();
            promptInputCapture = CreatePromptInputCapture();
            finger = CreateFinger();
            var instructionBubble = CreateInstructionBubble(out instructionLabel);

            root.Add(focusMask);
            root.Add(focusHighlight);
            root.Add(finger);
            root.Add(instructionBubble);
            root.Add(promptInputCapture);

            return root;
        }

        private void ApplyFocusStyle(TutorialAction focusAction)
        {
            switch (focusAction)
            {
                case TutorialAction.Jump:
                case TutorialAction.SuperJump:
                    ApplyJumpFocusStyle(showFinger: false);
                    break;
                case TutorialAction.Ultra:
                    ApplyUltraFocusStyle(showFinger: false);
                    break;
                default:
                    ApplyTapFocusStyle(showFinger: true);
                    break;
            }
        }

        private void ApplyTapFocusStyle(bool showFinger)
        {
            Rect tapRect = GetTargetRect(_tapTarget, new Rect(0f, 0.3f, 0.6f, 0.7f), false);
            ApplyFocusRect(tapRect, TutorialFocusShape.RoundedRect, showFinger);
        }

        private void ApplyJumpFocusStyle(bool showFinger)
        {
            ApplyFocusRect(
                GetTargetRect(_jumpTarget, new Rect(0.78f, 0.58f, 0.2f, 0.35f), true),
                TutorialFocusShape.Circle,
                showFinger);
        }

        private void ApplyUltraFocusStyle(bool showFinger)
        {
            ApplyFocusRect(
                GetTargetRect(_ultraTarget, new Rect(0.78f, 0.2f, 0.16f, 0.28f), true),
                TutorialFocusShape.Circle,
                showFinger);
        }

        private Rect GetTargetRect(VisualElement target, Rect fallbackNormalizedRect, bool addPadding)
        {
            Rect rootBounds = _root.worldBound;
            if (rootBounds.width <= 0f || rootBounds.height <= 0f)
            {
                return GetScreenFallbackRect(fallbackNormalizedRect);
            }

            if (target == null || target.worldBound.width <= 0f || target.worldBound.height <= 0f)
            {
                return GetNormalizedRootRect(fallbackNormalizedRect, rootBounds);
            }

            Rect targetBounds = target.worldBound;
            float padding = addPadding ? _focusPadding : 0f;
            return ClampRectToRoot(new Rect(
                targetBounds.x - rootBounds.x - padding,
                targetBounds.y - rootBounds.y - padding,
                targetBounds.width + padding * 2f,
                targetBounds.height + padding * 2f), rootBounds);
        }

        private static Rect GetScreenFallbackRect(Rect normalizedRect)
        {
            return new Rect(
                Screen.width * normalizedRect.x,
                Screen.height * normalizedRect.y,
                Screen.width * normalizedRect.width,
                Screen.height * normalizedRect.height);
        }

        private static Rect GetNormalizedRootRect(Rect normalizedRect, Rect rootBounds)
        {
            return new Rect(
                rootBounds.width * normalizedRect.x,
                rootBounds.height * normalizedRect.y,
                rootBounds.width * normalizedRect.width,
                rootBounds.height * normalizedRect.height);
        }

        private static Rect ClampRectToRoot(Rect rect, Rect rootBounds)
        {
            float xMin = Mathf.Clamp(rect.xMin, 0f, rootBounds.width);
            float yMin = Mathf.Clamp(rect.yMin, 0f, rootBounds.height);
            float xMax = Mathf.Clamp(rect.xMax, xMin, rootBounds.width);
            float yMax = Mathf.Clamp(rect.yMax, yMin, rootBounds.height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void ApplyFocusRect(Rect focusRect, TutorialFocusShape shape, bool showFinger)
        {
            _currentFocusRect = focusRect;
            _hasCurrentFocusRect = true;

            Rect rootBounds = _root.worldBound;
            float rootWidth = rootBounds.width > 0f ? rootBounds.width : Screen.width;
            float rootHeight = rootBounds.height > 0f ? rootBounds.height : Screen.height;
            Rect rootRect = new Rect(0f, 0f, rootWidth, rootHeight);

            ApplyFocusMask(focusRect, shape, rootRect);
            SetFocusElementRect(_focusHighlight, focusRect);
            ApplyFocusRadius(_focusHighlight, focusRect, shape, 0f);

            _finger.style.display = showFinger ? DisplayStyle.Flex : DisplayStyle.None;
            if (showFinger)
            {
                _finger.style.left = focusRect.x + focusRect.width * 0.5f - _fingerSize * 0.5f;
                _finger.style.top = focusRect.y + focusRect.height * 0.55f - _fingerSize * 0.5f;
                _finger.style.bottom = StyleKeyword.Auto;
                _finger.style.marginLeft = 0;
            }
        }

        private static VisualElement CreateCompleteRoot(
            out Label titleLabel,
            out Label messageLabel,
            out Button playButton,
            out Button menuButton)
        {
            var root = new VisualElement { name = _completeRootName, pickingMode = PickingMode.Position };
            FillScreen(root);
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            var container = CreateCompleteContainer();
            titleLabel = CreateLabel("tutorial-complete-title", 48, TextAnchor.MiddleCenter);
            titleLabel.style.marginBottom = 12;

            messageLabel = CreateLabel("tutorial-complete-message", 38, TextAnchor.MiddleCenter);
            messageLabel.style.marginBottom = 28;

            var buttons = new VisualElement { name = "tutorial-complete-buttons" };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.Center;

            playButton = CreateCompleteButton("tutorial-complete-play", "Играть");
            menuButton = CreateCompleteButton("tutorial-complete-menu", "Меню");
            playButton.style.marginRight = 20;
            menuButton.style.marginLeft = 20;

            buttons.Add(playButton);
            buttons.Add(menuButton);
            container.Add(titleLabel);
            container.Add(messageLabel);
            container.Add(buttons);
            root.Add(container);

            return root;
        }

        private static void RemoveExistingTutorialLayers(VisualElement contentRoot)
        {
            contentRoot.Q<VisualElement>(_rootName)?.RemoveFromHierarchy();
            contentRoot.Q<VisualElement>(_completeRootName)?.RemoveFromHierarchy();
        }

        private static VisualElement CreateFocusHighlight()
        {
            var highlight = new VisualElement { name = "tutorial-focus-highlight", pickingMode = PickingMode.Ignore };
            highlight.style.position = Position.Absolute;
            highlight.style.backgroundColor = Color.clear;
            return highlight;
        }

        private static VisualElement CreateFocusMask()
        {
            var mask = new VisualElement { name = "tutorial-focus-mask", pickingMode = PickingMode.Ignore };
            FillScreen(mask);
            return mask;
        }

        private static VisualElement CreatePromptInputCapture()
        {
            var capture = new VisualElement
            {
                name = "tutorial-prompt-input-capture",
                pickingMode = PickingMode.Position
            };
            FillScreen(capture);
            capture.style.backgroundColor = Color.clear;
            return capture;
        }

        private static VisualElement CreateFinger()
        {
            var finger = new VisualElement { name = "tutorial-finger", pickingMode = PickingMode.Ignore };
            finger.style.position = Position.Absolute;
            finger.style.left = Length.Percent(26);
            finger.style.bottom = Length.Percent(28);
            finger.style.width = _fingerSize;
            finger.style.height = _fingerSize;

            var fingerTexture = Resources.Load<Texture2D>(_fingerResourcePath);
            if (fingerTexture != null)
            {
                finger.style.backgroundImage = new StyleBackground(Background.FromTexture2D(fingerTexture));
            }

            return finger;
        }

        private static VisualElement CreateInstructionBubble(out Label instructionLabel)
        {
            var bubble = new VisualElement { name = "tutorial-instruction-bubble", pickingMode = PickingMode.Ignore };
            bubble.style.position = Position.Absolute;
            bubble.style.left = Length.Percent(50);
            bubble.style.top = Length.Percent(45);
            bubble.style.width = 620;
            bubble.style.minHeight = 112;
            bubble.style.marginLeft = -310;
            bubble.style.marginTop = -56;
            bubble.style.paddingTop = 20;
            bubble.style.paddingRight = 28;
            bubble.style.paddingBottom = 20;
            bubble.style.paddingLeft = 28;
            bubble.style.backgroundColor = new Color(0.98f, 0.92f, 0.45f, 0.96f);
            bubble.style.borderTopLeftRadius = 28;
            bubble.style.borderTopRightRadius = 28;
            bubble.style.borderBottomRightRadius = 28;
            bubble.style.borderBottomLeftRadius = 28;

            instructionLabel = CreateLabel("tutorial-instruction", 42, TextAnchor.MiddleCenter);
            instructionLabel.style.whiteSpace = WhiteSpace.Normal;
            bubble.Add(instructionLabel);

            return bubble;
        }

        private static VisualElement CreateCompleteContainer()
        {
            var container = new VisualElement { name = "tutorial-complete-container" };
            container.style.width = 620;
            container.style.paddingTop = 32;
            container.style.paddingRight = 36;
            container.style.paddingBottom = 36;
            container.style.paddingLeft = 36;
            container.style.backgroundColor = new Color(0.98f, 0.92f, 0.45f, 0.98f);
            container.style.borderTopLeftRadius = 28;
            container.style.borderTopRightRadius = 28;
            container.style.borderBottomRightRadius = 28;
            container.style.borderBottomLeftRadius = 28;
            container.style.alignItems = Align.Center;
            return container;
        }

        private static Button CreateCompleteButton(string name, string text)
        {
            var button = CreateButton(name, text);
            button.style.width = 220;
            button.style.height = 84;
            return button;
        }

        private static Label CreateLabel(string name, int fontSize, TextAnchor align)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = align;
            label.style.unityTextOutlineWidth = 3;
            label.style.unityTextOutlineColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            return label;
        }

        private static Button CreateButton(string name, string text)
        {
            var button = new Button { name = name, text = text };
            button.style.backgroundColor = new Color(0.85f, 0.9f, 0.6f, 1f);
            button.style.borderTopWidth = 6;
            button.style.borderRightWidth = 6;
            button.style.borderBottomWidth = 6;
            button.style.borderLeftWidth = 6;
            button.style.borderTopColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            button.style.borderRightColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            button.style.borderBottomColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            button.style.borderLeftColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            button.style.borderTopLeftRadius = 20;
            button.style.borderTopRightRadius = 20;
            button.style.borderBottomRightRadius = 20;
            button.style.borderBottomLeftRadius = 20;
            button.style.color = Color.white;
            button.style.fontSize = 38;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextOutlineWidth = 2;
            button.style.unityTextOutlineColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            return button;
        }

        private static void FillScreen(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
            element.style.left = 0;
        }

        private void ApplyFocusMask(Rect focusRect, TutorialFocusShape shape, Rect rootRect)
        {
            var texture = TutorialFocusMaskBuilder.Create(
                focusRect,
                shape,
                rootRect,
                _dimAlpha,
                _softFocusWidth,
                _focusMaskMaxWidth);
            ReplaceFocusMaskTexture(texture);
        }

        private void ReplaceFocusMaskTexture(Texture2D texture)
        {
            ClearFocusMaskTexture();
            _focusMaskTexture = texture;
            _focusMask.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
        }

        private void ClearFocusMaskTexture()
        {
            if (_focusMaskTexture != null)
            {
                UnityEngine.Object.Destroy(_focusMaskTexture);
                _focusMaskTexture = null;
            }

            if (_focusMask != null)
            {
                _focusMask.style.backgroundImage = null;
            }
        }

        private static void SetFocusElementRect(VisualElement element, Rect rect)
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
            TutorialFocusShape shape,
            float expansion)
        {
            float radius = shape == TutorialFocusShape.Circle
                ? Mathf.Min(rect.width, rect.height) * 0.5f
                : 28f + expansion;

            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
        }

    }
}
