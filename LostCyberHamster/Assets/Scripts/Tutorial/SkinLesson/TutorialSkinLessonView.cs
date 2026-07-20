using System;
using System.Linq;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Находит UI-цель урока скина, рисует подсказку и пропускает ввод только в эту цель.
    /// </summary>
    public sealed class TutorialSkinLessonView : IDisposable
    {
        private const string _focusRootName = "tutorial-skin-focus-root";
        private const float _focusPadding = 18f;
        private const float _dimAlpha = 0.62f;
        private const float _softFocusWidth = 48f;
        private const int _focusMaskMaxWidth = 512;

        private readonly TutorialFocusMaskBuilder _focusMaskBuilder = new();
        private readonly TutorialUiInputBlocker _inputBlocker = new();
        private readonly TutorialUiAutomation _automation = new();

        private TutorialSkinStep _activeStep;
        private VisualElement _activeRoot;
        private VisualElement _activeTarget;
        private VisualElement _focusRoot;
        private VisualElement _focusMask;
        private VisualElement _focusHighlight;
        private int _bindingVersion;
        private bool _isDisposed;

        internal event Action<TutorialSkinAction> AllowedActionPerformed;

        /// <summary>
        /// Показывает шаг на доступном UI-экране или ждёт появления его target.
        /// </summary>
        internal void Show(TutorialSkinStep step)
        {
            ThrowIfDisposed();
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            if (!TryFindPromptTarget(step.Prompt, out var root, out var target))
            {
                if (_activeStep != step || _activeTarget?.panel == null)
                {
                    ClearBinding();
                }

                return;
            }

            if (_activeStep == step && _activeRoot == root && _activeTarget == target)
            {
                return;
            }

            Bind(step, root, target);
        }

        /// <summary>
        /// Проверяет наличие UI-экрана по его устойчивым элементам.
        /// </summary>
        internal bool IsSurfaceVisible(ScreenEnum surface)
        {
            ThrowIfDisposed();
            return TryFindSurfaceRoot(surface, out _);
        }

        /// <summary>
        /// Проверяет фактически показанный CharacterScreen skin после штатного обработчика кнопки.
        /// </summary>
        internal bool IsSkinDisplayed(int skinId)
        {
            ThrowIfDisposed();
            if (!TryFindSurfaceRoot(ScreenEnum.CharacterScreen, out var root))
            {
                return false;
            }

            var skin = SkinManager.AvailableSkins.FirstOrDefault(candidate => candidate.Id == skinId);
            string displayedSkinName = root.Q<Label>("skin-name")?.text;
            return skin != null && displayedSkinName == skin.Name;
        }

        /// <summary>
        /// Повторно планирует automation-click, когда текущий шаг ещё не выполнен.
        /// </summary>
        internal void RepeatAutomation()
        {
            ThrowIfDisposed();
            if (_activeTarget?.panel != null)
            {
                _automation.Schedule(_activeTarget);
            }
        }

        /// <summary>
        /// Сбрасывает текущую подсветку и подписки, сохраняя view пригодной для нового запуска.
        /// </summary>
        public void Reset()
        {
            if (_isDisposed)
            {
                return;
            }

            ClearBinding();
        }

        /// <summary>
        /// Удаляет tutorial UI, подписки и runtime-текстуру focus mask.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            ClearBinding();
            _automation.Dispose();
            _focusMaskBuilder.Dispose();
            AllowedActionPerformed = null;
            _isDisposed = true;
        }

        private void Bind(TutorialSkinStep step, VisualElement root, VisualElement target)
        {
            // Снимаем старые UI-подписки до привязки к новому screen root.
            ClearBinding();
            _activeStep = step;
            _activeRoot = root;
            _activeTarget = target;
            _bindingVersion++;

            // Overlay не перехватывает target; input blocker режет только чужой pointer down.
            _inputBlocker.Attach(root, target);
            root.RegisterCallback<ClickEvent>(ObserveAllowedClick, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(BlockUnexpectedNavigationSubmit, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(BlockNavigationCancel, TrickleDown.TrickleDown);
            CreateFocusOverlay(root, step.Prompt.Instruction);
            ScheduleFocusRefresh(0);
            ScheduleFocusRefresh(100);
            _automation.Schedule(target);
        }

        private void ObserveAllowedClick(ClickEvent evt)
        {
            if (_activeStep == null)
            {
                return;
            }

            if (!IsEventInsideTarget(evt, _activeTarget))
            {
                evt.StopImmediatePropagation();
                return;
            }

            // Проверка идёт следующим UI-tick: штатный handler кнопки успевает изменить game/UI state.
            int bindingVersion = _bindingVersion;
            TutorialSkinAction action = _activeStep.Action;
            _activeRoot.schedule.Execute(() => NotifyAllowedAction(action, bindingVersion));
        }

        private void BlockUnexpectedNavigationSubmit(NavigationSubmitEvent evt)
        {
            if (!IsEventInsideTarget(evt, _activeTarget))
            {
                evt.StopImmediatePropagation();
            }
        }

        private static void BlockNavigationCancel(NavigationCancelEvent evt)
        {
            evt.StopImmediatePropagation();
        }

        private void NotifyAllowedAction(TutorialSkinAction action, int bindingVersion)
        {
            if (!_isDisposed && bindingVersion == _bindingVersion)
            {
                AllowedActionPerformed?.Invoke(action);
            }
        }

        private void CreateFocusOverlay(VisualElement documentRoot, string instruction)
        {
            documentRoot.Q<VisualElement>(_focusRootName)?.RemoveFromHierarchy();

            _focusRoot = new VisualElement { name = _focusRootName, pickingMode = PickingMode.Ignore };
            FillScreen(_focusRoot);

            _focusMask = new VisualElement
            {
                name = "tutorial-skin-focus-mask",
                pickingMode = PickingMode.Ignore
            };
            FillScreen(_focusMask);

            _focusHighlight = new VisualElement
            {
                name = "tutorial-skin-focus-highlight",
                pickingMode = PickingMode.Ignore
            };
            _focusHighlight.style.position = Position.Absolute;
            _focusHighlight.style.backgroundColor = Color.clear;

            _focusRoot.Add(_focusMask);
            _focusRoot.Add(_focusHighlight);
            _focusRoot.Add(CreateInstructionBubble(instruction));
            documentRoot.Add(_focusRoot);
        }

        private void ScheduleFocusRefresh(long delayMs)
        {
            int bindingVersion = _bindingVersion;
            _focusRoot.schedule.Execute(() => ApplyFocus(bindingVersion)).ExecuteLater(delayMs);
        }

        private void ApplyFocus(int bindingVersion)
        {
            if (_isDisposed
                || bindingVersion != _bindingVersion
                || _activeRoot?.panel == null
                || _activeTarget?.panel == null)
            {
                return;
            }

            Rect rootBounds = _activeRoot.worldBound;
            Rect targetBounds = _activeTarget.worldBound;
            if (rootBounds.width <= 0f
                || rootBounds.height <= 0f
                || targetBounds.width <= 0f
                || targetBounds.height <= 0f)
            {
                return;
            }

            Rect focusRect = GetTargetRect(rootBounds, targetBounds);
            Rect rootRect = new Rect(0f, 0f, rootBounds.width, rootBounds.height);

            // Сначала снимаем StyleBackground, затем builder уничтожает предыдущую texture.
            _focusMask.style.backgroundImage = null;
            Texture2D texture = _focusMaskBuilder.Build(
                focusRect,
                _activeStep.Prompt.Shape,
                rootRect,
                _dimAlpha,
                _softFocusWidth,
                _focusMaskMaxWidth);
            _focusMask.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
            SetElementRect(_focusHighlight, focusRect);
            ApplyFocusRadius(_focusHighlight, focusRect, _activeStep.Prompt.Shape);
        }

        private void ClearBinding()
        {
            _bindingVersion++;
            _automation.Cancel();
            _inputBlocker.Detach();

            if (_activeRoot != null)
            {
                _activeRoot.UnregisterCallback<ClickEvent>(ObserveAllowedClick, TrickleDown.TrickleDown);
                _activeRoot.UnregisterCallback<NavigationSubmitEvent>(
                    BlockUnexpectedNavigationSubmit,
                    TrickleDown.TrickleDown);
                _activeRoot.UnregisterCallback<NavigationCancelEvent>(
                    BlockNavigationCancel,
                    TrickleDown.TrickleDown);
            }

            if (_focusMask != null)
            {
                _focusMask.style.backgroundImage = null;
            }

            _focusMaskBuilder.Clear();
            _focusRoot?.RemoveFromHierarchy();
            _activeStep = null;
            _activeRoot = null;
            _activeTarget = null;
            _focusRoot = null;
            _focusMask = null;
            _focusHighlight = null;
        }

        private static bool TryFindPromptTarget(
            TutorialSkinPrompt prompt,
            out VisualElement root,
            out VisualElement target)
        {
            root = null;
            target = null;
            foreach (var uiDocument in UnityEngine.Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                VisualElement candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null || !ContainsSurfaceMarker(candidateRoot, prompt.Surface))
                {
                    continue;
                }

                VisualElement candidateTarget = ResolveTarget(candidateRoot, prompt.Target);
                if (candidateTarget?.panel == null)
                {
                    continue;
                }

                root = candidateRoot;
                target = candidateTarget;
                return true;
            }

            return false;
        }

        private static bool TryFindSurfaceRoot(ScreenEnum surface, out VisualElement root)
        {
            root = null;
            foreach (var uiDocument in UnityEngine.Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                VisualElement candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null || !ContainsSurfaceMarker(candidateRoot, surface))
                {
                    continue;
                }

                root = candidateRoot;
                return true;
            }

            return false;
        }

        private static bool ContainsSurfaceMarker(VisualElement root, ScreenEnum surface)
        {
            return surface switch
            {
                ScreenEnum.HomeScreen => root.Q<VisualElement>("btn_character") != null,
                ScreenEnum.CharacterScreen => root.Q<VisualElement>("btn-skin-next") != null,
                _ => false
            };
        }

        private static VisualElement ResolveTarget(VisualElement root, TutorialSkinTarget target)
        {
            return target switch
            {
                TutorialSkinTarget.HomeCharacterButton => root.Q<VisualElement>("btn_character"),
                TutorialSkinTarget.SkinNextButton => root.Q<VisualElement>("btn-skin-next"),
                TutorialSkinTarget.SkinChangeButton => root.Q<VisualElement>("skin-btn-change"),
                _ => null
            };
        }

        private static bool IsEventInsideTarget(EventBase evt, VisualElement target)
        {
            if (evt.target is not VisualElement element || target == null)
            {
                return false;
            }

            while (element != null)
            {
                if (element == target)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private static Rect GetTargetRect(Rect rootBounds, Rect targetBounds)
        {
            var focusRect = new Rect(
                targetBounds.x - rootBounds.x - _focusPadding,
                targetBounds.y - rootBounds.y - _focusPadding,
                targetBounds.width + _focusPadding * 2f,
                targetBounds.height + _focusPadding * 2f);
            float xMin = Mathf.Clamp(focusRect.xMin, 0f, rootBounds.width);
            float yMin = Mathf.Clamp(focusRect.yMin, 0f, rootBounds.height);
            float xMax = Mathf.Clamp(focusRect.xMax, xMin, rootBounds.width);
            float yMax = Mathf.Clamp(focusRect.yMax, yMin, rootBounds.height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static VisualElement CreateInstructionBubble(string instruction)
        {
            var bubble = new VisualElement
            {
                name = "tutorial-skin-instruction-bubble",
                pickingMode = PickingMode.Ignore
            };
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

            var label = new Label(instruction)
            {
                name = "tutorial-skin-instruction",
                pickingMode = PickingMode.Ignore
            };
            label.style.color = Color.white;
            label.style.fontSize = 38;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityTextOutlineWidth = 3;
            label.style.unityTextOutlineColor = new Color(0.13f, 0.51f, 0.53f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            bubble.Add(label);
            return bubble;
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

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TutorialSkinLessonView));
            }
        }

        private sealed class TutorialUiInputBlocker
        {
            private VisualElement _root;
            private VisualElement _allowedTarget;

            public void Attach(VisualElement root, VisualElement allowedTarget)
            {
                Detach();
                _root = root;
                _allowedTarget = allowedTarget;
                _root.RegisterCallback<PointerDownEvent>(BlockUnexpectedPointerDown, TrickleDown.TrickleDown);
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
                if (!IsEventInsideTarget(evt, _allowedTarget))
                {
                    evt.StopImmediatePropagation();
                }
            }
        }
    }
}
