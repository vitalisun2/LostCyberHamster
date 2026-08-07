using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Отображает gameplay-подсказки tutorial и передаёт UI-ввод наружу.
    /// </summary>
    public sealed class TutorialGameplayView : IDisposable
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
        private readonly Button _primaryCompletionButton;
        private readonly Button _secondaryCompletionButton;
        private readonly TutorialFocusOverlay _focusOverlay = new();

        private TutorialAction _currentPromptAction;
        private Rect _currentFocusRect;
        private bool _hasCurrentFocusRect;
        private int _focusVersion;
        private bool _isDisposed;

        public TutorialGameplayView(VisualElement contentRoot)
        {
            if (contentRoot == null)
            {
                throw new ArgumentNullException(nameof(contentRoot));
            }

            _tapTarget = contentRoot.Q<VisualElement>("tap");
            _jumpTarget = contentRoot.Q<VisualElement>("btn_jump");
            _ultraTarget = contentRoot.Q<VisualElement>("btn_ultra");
            RemoveExistingTutorialLayers(contentRoot);

            _root = CreateRoot();
            _titleLabel = CreateTitle();
            _skipButton = CreateSkipButton();
            _idleInputBlocker = CreateInputBlocker();
            _promptRoot = CreatePromptRoot(
                out _focusMask,
                out _focusHighlight,
                out _promptInputCapture,
                out _finger,
                out _instructionLabel);
            _completeRoot = CreateCompletionRoot(
                out _completeTitleLabel,
                out _completeMessageLabel,
                out _primaryCompletionButton,
                out _secondaryCompletionButton);

            _root.Add(_idleInputBlocker);
            _root.Add(_promptRoot);
            _root.Add(_titleLabel);
            _root.Add(_skipButton);
            contentRoot.Add(_root);
            contentRoot.Add(_completeRoot);

            RegisterCallbacks();
            Hide();
        }

        public event Action<TutorialAction> GameplayActionRequested;
        public event Action SkipRequested;
        public event Action PrimaryCompletionRequested;
        public event Action SecondaryCompletionRequested;

        public void ShowHeader(string title)
        {
            _titleLabel.text = title ?? string.Empty;
            _completeRoot.style.display = DisplayStyle.None;
            _root.style.display = DisplayStyle.Flex;
            _idleInputBlocker.style.display = DisplayStyle.Flex;
            _skipButton.style.display = DisplayStyle.Flex;
            HidePrompt();
        }

        public void ShowPrompt(string instruction, TutorialAction focusAction)
        {
            _instructionLabel.text = instruction ?? string.Empty;
            _currentPromptAction = focusAction;
            _root.style.display = DisplayStyle.Flex;
            _idleInputBlocker.style.display = DisplayStyle.None;
            _promptRoot.style.display = DisplayStyle.Flex;
            _hasCurrentFocusRect = false;
            int focusVersion = ++_focusVersion;
            _promptRoot.schedule.Execute(() => ApplyFocusStyle(focusAction, focusVersion)).ExecuteLater(0);
        }

        public void HidePrompt()
        {
            _promptRoot.style.display = DisplayStyle.None;
            _focusVersion++;
            _hasCurrentFocusRect = false;
            _focusOverlay.Clear();
        }

        public void ShowCompletion(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText,
            bool showPrimaryButton)
        {
            Hide();
            _completeTitleLabel.text = title ?? string.Empty;
            _completeMessageLabel.text = message ?? string.Empty;
            _primaryCompletionButton.text = primaryButtonText ?? string.Empty;
            _secondaryCompletionButton.text = secondaryButtonText ?? string.Empty;
            _primaryCompletionButton.style.display = showPrimaryButton
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _completeRoot.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _completeRoot.style.display = DisplayStyle.None;
            _idleInputBlocker.style.display = DisplayStyle.None;
            _focusVersion++;
            _hasCurrentFocusRect = false;
            _focusOverlay.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UnregisterCallbacks();
            _focusOverlay.Dispose();
            _root.RemoveFromHierarchy();
            _completeRoot.RemoveFromHierarchy();
            GameplayActionRequested = null;
            SkipRequested = null;
            PrimaryCompletionRequested = null;
            SecondaryCompletionRequested = null;
        }

        private void RegisterCallbacks()
        {
            _skipButton.RegisterCallback<ClickEvent>(HandleSkipClicked);
            _primaryCompletionButton.RegisterCallback<ClickEvent>(HandlePrimaryCompletionClicked);
            _secondaryCompletionButton.RegisterCallback<ClickEvent>(HandleSecondaryCompletionClicked);
            _promptInputCapture.RegisterCallback<PointerDownEvent>(HandlePromptPointerDown);
        }

        private void UnregisterCallbacks()
        {
            _skipButton.UnregisterCallback<ClickEvent>(HandleSkipClicked);
            _primaryCompletionButton.UnregisterCallback<ClickEvent>(HandlePrimaryCompletionClicked);
            _secondaryCompletionButton.UnregisterCallback<ClickEvent>(HandleSecondaryCompletionClicked);
            _promptInputCapture.UnregisterCallback<PointerDownEvent>(HandlePromptPointerDown);
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

        private void HandlePromptPointerDown(PointerDownEvent evt)
        {
            if (_hasCurrentFocusRect && _currentFocusRect.Contains(evt.localPosition))
            {
                GameplayActionRequested?.Invoke(_currentPromptAction);
            }

            evt.StopImmediatePropagation();
        }

        private void ApplyFocusStyle(TutorialAction focusAction, int focusVersion)
        {
            if (_isDisposed || focusVersion != _focusVersion || _promptRoot.panel == null)
            {
                return;
            }

            switch (focusAction)
            {
                case TutorialAction.Jump:
                case TutorialAction.SuperJump:
                    ApplyFocusRect(
                        GetTargetRect(_jumpTarget, new Rect(0.78f, 0.58f, 0.2f, 0.35f), true),
                        TutorialFocusShape.Circle,
                        showFinger: false);
                    break;
                case TutorialAction.Ultra:
                    ApplyFocusRect(
                        GetTargetRect(_ultraTarget, new Rect(0.78f, 0.2f, 0.16f, 0.28f), true),
                        TutorialFocusShape.Circle,
                        showFinger: false);
                    break;
                default:
                    ApplyFocusRect(
                        GetTargetRect(_tapTarget, new Rect(0f, 0.3f, 0.6f, 0.7f), false),
                        TutorialFocusShape.RoundedRect,
                        showFinger: true);
                    break;
            }
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
            return TutorialFocusOverlay.GetTargetRect(rootBounds, targetBounds, padding);
        }

        private void ApplyFocusRect(Rect focusRect, TutorialFocusShape shape, bool showFinger)
        {
            _currentFocusRect = focusRect;
            _hasCurrentFocusRect = true;

            Rect rootBounds = _root.worldBound;
            float rootWidth = rootBounds.width > 0f ? rootBounds.width : Screen.width;
            float rootHeight = rootBounds.height > 0f ? rootBounds.height : Screen.height;
            var rootRect = new Rect(0f, 0f, rootWidth, rootHeight);

            _focusOverlay.Apply(
                _focusMask,
                _focusHighlight,
                focusRect,
                shape,
                rootRect,
                _dimAlpha,
                _softFocusWidth,
                _focusMaskMaxWidth);
            PositionFinger(focusRect, showFinger);
        }

        private void PositionFinger(Rect focusRect, bool showFinger)
        {
            _finger.style.display = showFinger ? DisplayStyle.Flex : DisplayStyle.None;
            if (!showFinger)
            {
                return;
            }

            _finger.style.left = focusRect.x + focusRect.width * 0.5f - _fingerSize * 0.5f;
            _finger.style.top = focusRect.y + focusRect.height * 0.55f - _fingerSize * 0.5f;
            _finger.style.bottom = StyleKeyword.Auto;
            _finger.style.marginLeft = 0;
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement { name = _rootName, pickingMode = PickingMode.Ignore };
            FillScreen(root);
            return root;
        }

        private static Label CreateTitle()
        {
            Label title = CreateLabel("tutorial-title", 44, TextAnchor.MiddleCenter);
            title.style.position = Position.Absolute;
            title.style.top = 18;
            title.style.left = 0;
            title.style.width = Length.Percent(100);
            title.style.height = 76;
            return title;
        }

        private static Button CreateSkipButton()
        {
            Button button = CreateButton("tutorial-skip", "Пропустить");
            button.pickingMode = PickingMode.Position;
            button.style.position = Position.Absolute;
            button.style.bottom = 24;
            button.style.left = Length.Percent(50);
            button.style.marginLeft = -150;
            button.style.width = 300;
            button.style.height = 82;
            return button;
        }

        private static VisualElement CreateInputBlocker()
        {
            var blocker = new VisualElement
            {
                name = "tutorial-idle-input-blocker",
                pickingMode = PickingMode.Position
            };
            FillScreen(blocker);
            blocker.AddToClassList("tutorial-transparent");
            return blocker;
        }

        private static VisualElement CreatePromptRoot(
            out VisualElement focusMask,
            out VisualElement focusHighlight,
            out VisualElement inputCapture,
            out VisualElement finger,
            out Label instructionLabel)
        {
            var root = new VisualElement { name = "tutorial-prompt-root", pickingMode = PickingMode.Ignore };
            FillScreen(root);

            focusMask = CreateFocusMask();
            focusHighlight = CreateFocusHighlight();
            inputCapture = CreatePromptInputCapture();
            finger = CreateFinger();
            VisualElement instructionBubble = CreateInstructionBubble(out instructionLabel);

            root.Add(focusMask);
            root.Add(focusHighlight);
            root.Add(finger);
            root.Add(instructionBubble);
            root.Add(inputCapture);
            return root;
        }

        private static VisualElement CreateCompletionRoot(
            out Label titleLabel,
            out Label messageLabel,
            out Button primaryButton,
            out Button secondaryButton)
        {
            var root = new VisualElement { name = _completeRootName, pickingMode = PickingMode.Position };
            FillScreen(root);
            root.AddToClassList("tutorial-overlay");
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            VisualElement container = CreateCompletionContainer();
            titleLabel = CreateLabel("tutorial-complete-title", 48, TextAnchor.MiddleCenter);
            titleLabel.style.marginBottom = 12;
            messageLabel = CreateLabel("tutorial-complete-message", 38, TextAnchor.MiddleCenter);
            messageLabel.style.marginBottom = 28;

            var buttons = new VisualElement { name = "tutorial-complete-buttons" };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.Center;
            primaryButton = CreateCompletionButton("tutorial-complete-play", "Играть");
            secondaryButton = CreateCompletionButton("tutorial-complete-menu", "Меню");
            primaryButton.style.marginRight = 20;
            secondaryButton.style.marginLeft = 20;

            buttons.Add(primaryButton);
            buttons.Add(secondaryButton);
            container.Add(titleLabel);
            container.Add(messageLabel);
            container.Add(buttons);
            root.Add(container);
            return root;
        }

        private static VisualElement CreateFocusMask()
        {
            var mask = new VisualElement { name = "tutorial-focus-mask", pickingMode = PickingMode.Ignore };
            FillScreen(mask);
            return mask;
        }

        private static VisualElement CreateFocusHighlight()
        {
            var highlight = new VisualElement
            {
                name = "tutorial-focus-highlight",
                pickingMode = PickingMode.Ignore
            };
            highlight.style.position = Position.Absolute;
            highlight.AddToClassList("tutorial-transparent");
            return highlight;
        }

        private static VisualElement CreatePromptInputCapture()
        {
            var capture = new VisualElement
            {
                name = "tutorial-prompt-input-capture",
                pickingMode = PickingMode.Position
            };
            FillScreen(capture);
            capture.AddToClassList("tutorial-transparent");
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

            Texture2D fingerTexture = Resources.Load<Texture2D>(_fingerResourcePath);
            if (fingerTexture != null)
            {
                finger.style.backgroundImage = new StyleBackground(Background.FromTexture2D(fingerTexture));
            }

            return finger;
        }

        private static VisualElement CreateInstructionBubble(out Label instructionLabel)
        {
            var bubble = new VisualElement
            {
                name = "tutorial-instruction-bubble",
                pickingMode = PickingMode.Ignore
            };
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
            bubble.AddToClassList("tutorial-surface");
            bubble.style.borderTopLeftRadius = 28;
            bubble.style.borderTopRightRadius = 28;
            bubble.style.borderBottomRightRadius = 28;
            bubble.style.borderBottomLeftRadius = 28;

            instructionLabel = CreateLabel("tutorial-instruction", 42, TextAnchor.MiddleCenter);
            instructionLabel.style.whiteSpace = WhiteSpace.Normal;
            bubble.Add(instructionLabel);
            return bubble;
        }

        private static VisualElement CreateCompletionContainer()
        {
            var container = new VisualElement { name = "tutorial-complete-container" };
            container.style.width = 620;
            container.style.paddingTop = 32;
            container.style.paddingRight = 36;
            container.style.paddingBottom = 36;
            container.style.paddingLeft = 36;
            container.AddToClassList("tutorial-surface");
            container.style.borderTopLeftRadius = 28;
            container.style.borderTopRightRadius = 28;
            container.style.borderBottomRightRadius = 28;
            container.style.borderBottomLeftRadius = 28;
            container.style.alignItems = Align.Center;
            return container;
        }

        private static Button CreateCompletionButton(string name, string text)
        {
            Button button = CreateButton(name, text);
            button.style.width = 220;
            button.style.height = 84;
            return button;
        }

        private static Label CreateLabel(string name, int fontSize, TextAnchor alignment)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList("tutorial-text");
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = alignment;
            label.style.unityTextOutlineWidth = 3;
            return label;
        }

        private static Button CreateButton(string name, string text)
        {
            var button = new Button { name = name, text = text };
            button.AddToClassList("tutorial-button");
            button.style.borderTopWidth = 6;
            button.style.borderRightWidth = 6;
            button.style.borderBottomWidth = 6;
            button.style.borderLeftWidth = 6;
            button.style.borderTopLeftRadius = 20;
            button.style.borderTopRightRadius = 20;
            button.style.borderBottomRightRadius = 20;
            button.style.borderBottomLeftRadius = 20;
            button.style.fontSize = 38;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextOutlineWidth = 2;
            return button;
        }

        private static void RemoveExistingTutorialLayers(VisualElement contentRoot)
        {
            contentRoot.Q<VisualElement>(_rootName)?.RemoveFromHierarchy();
            contentRoot.Q<VisualElement>(_completeRootName)?.RemoveFromHierarchy();
        }

        private static void FillScreen(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
            element.style.left = 0;
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
            return TutorialFocusOverlay.ClampToRoot(rect, rootBounds.width, rootBounds.height);
        }
    }
}
