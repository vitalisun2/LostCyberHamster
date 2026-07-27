#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.Account;
using Assets.Scripts.DevTools.GameProgressTesting;
using Assets.Scripts.DevTools.Gameplay;
using Assets.Scripts.DevTools.Root;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Создаёт общую визуальную оболочку DEV-меню, размещает feature-экраны и управляет их навигацией.
    /// </summary>
    internal sealed class DevToolsOverlayShell
    {
        private const int _sortingOrder = 32767;
        private const float _baseMargin = 10f;
        private const float _baseOpenButtonWidth = 64f;
        private const float _baseHeaderHeight = 40f;
        private const float _baseRootPanelWidth = 300f;
        private const float _baseRootPanelHeight = 220f;
        private const float _baseFeaturePanelWidth = 540f;
        private const float _baseFeaturePanelHeight = 569f;
        private const float _baseMinimumPanelWidth = 300f;
        private const float _baseMinimumPanelHeight = 220f;
        private const float _baseInset = 16f;
        private const float _baseBackButtonWidth = 88f;
        private const float _baseResizeHandleSize = 44f;

        private static readonly Color _openButtonColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color _panelColor = new Color(1f, 1f, 1f, 0.985f);

        private readonly GameObject _host;
        private readonly GameObject _openButtonObject;
        private readonly RectTransform _openButtonRect;
        private readonly GameObject _panelObject;
        private readonly RectTransform _panelRect;
        private readonly RectTransform _dragHandleRect;
        private readonly RectTransform _resizeHandleRect;
        private readonly Text _titleText;
        private readonly RectTransform _titleRect;
        private readonly GameObject _backButtonObject;
        private readonly RectTransform _backButtonRect;
        private readonly RectTransform _closeButtonRect;
        private readonly RootDevToolsScreen _rootScreen;
        private readonly AccountDevToolsScreen _accountScreen;
        private readonly GameplayDevToolsScreen _gameplayScreen;

        private GameObject _ownedEventSystemObject;
        private IDevToolsScreen _activeScreen;
        private bool _isPanelOpen;
        private bool _isFeatureScreenOpen;
        private bool _hasPanelLayout;
        private bool _hasUserPanelSize;
        private bool _layoutWasFeatureScreen;
        private Vector2 _panelTopLeft;
        private Vector2 _panelSize;

        public DevToolsOverlayShell(GameObject host, AccountService accountService)
        {
            _host = host;
            EnsureEventSystem();

            Font font = LoadDefaultFont();
            Canvas canvas = host.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = host.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            if (host.GetComponent<GraphicRaycaster>() == null)
            {
                host.AddComponent<GraphicRaycaster>();
            }

            DevToolsUiFactory ui = new DevToolsUiFactory(font);
            Button openButton = ui.CreateButton(
                "OpenButton",
                host.transform,
                "DEV",
                _openButtonColor,
                OpenPanel,
                _baseHeaderHeight);
            _openButtonObject = openButton.gameObject;
            _openButtonRect = openButton.GetComponent<RectTransform>();

            _panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelObject.transform.SetParent(host.transform, false);
            _panelRect = _panelObject.GetComponent<RectTransform>();
            Image panelImage = _panelObject.GetComponent<Image>();
            panelImage.color = _panelColor;
            panelImage.raycastTarget = true;

            GameObject dragHandle = new GameObject(
                "DragHandle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(DevToolsPointerDragHandle));
            dragHandle.transform.SetParent(_panelObject.transform, false);
            Image dragHandleImage = dragHandle.GetComponent<Image>();
            dragHandleImage.color = new Color(1f, 1f, 1f, 0.001f);
            dragHandleImage.raycastTarget = true;
            dragHandle.GetComponent<DevToolsPointerDragHandle>().Configure(MovePanel);
            _dragHandleRect = dragHandle.GetComponent<RectTransform>();

            _titleText = ui.CreateText(
                "Title",
                _panelObject.transform,
                "Developer",
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _titleRect = _titleText.GetComponent<RectTransform>();
            Button backButton = ui.CreateButton(
                "BackButton",
                _panelObject.transform,
                "Назад",
                Color.white,
                NavigateBack,
                _baseHeaderHeight);
            _backButtonObject = backButton.gameObject;
            _backButtonRect = backButton.GetComponent<RectTransform>();
            Button closeButton = ui.CreateButton(
                "CloseButton",
                _panelObject.transform,
                "X",
                Color.white,
                ClosePanel,
                _baseHeaderHeight);
            _closeButtonRect = closeButton.GetComponent<RectTransform>();

            _rootScreen = new RootDevToolsScreen(
                _panelObject.transform,
                font,
                ClosePanel,
                ShowAccountScreen,
                ShowGameplayScreen,
                SetTitle);
            _accountScreen = new AccountDevToolsScreen(
                _panelObject.transform,
                font,
                accountService,
                ShowRootScreen,
                SetTitle);
            _gameplayScreen = new GameplayDevToolsScreen(
                _panelObject.transform,
                font,
                ShowRootScreen,
                SetTitle);

            Button resizeHandle = ui.CreateButton(
                "ResizeHandle",
                _panelObject.transform,
                "↘",
                DevToolsTheme.Navigation,
                () => { },
                _baseResizeHandleSize);
            resizeHandle.navigation = new Navigation { mode = Navigation.Mode.None };
            resizeHandle.gameObject
                .AddComponent<DevToolsPointerDragHandle>()
                .Configure(ResizePanel);
            _resizeHandleRect = resizeHandle.GetComponent<RectTransform>();
            resizeHandle.transform.SetAsLastSibling();

            ShowRootScreen();
            SetPanelOpen(false);
            ApplyLayout();
        }

        public void Tick()
        {
            EnsureEventSystem();
            ApplyLayout();
            GameProgressTestRunner.Shared.Tick();
            if (_isPanelOpen)
            {
                _activeScreen?.RefreshPresentation();
            }
        }

        private void EnsureEventSystem()
        {
            EventSystem ownedEventSystem = _ownedEventSystemObject != null
                ? _ownedEventSystemObject.GetComponent<EventSystem>()
                : null;
            if (ownedEventSystem != null)
            {
                if (EventSystem.current != null && EventSystem.current != ownedEventSystem)
                {
                    Object.Destroy(_ownedEventSystemObject);
                    _ownedEventSystemObject = null;
                }

                return;
            }

            if (EventSystem.current != null)
            {
                return;
            }

            _ownedEventSystemObject = new GameObject("[DevToolsEventSystem]", typeof(EventSystem));
            _ownedEventSystemObject.transform.SetParent(_host.transform, false);
#if ENABLE_INPUT_SYSTEM
            _ownedEventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            _ownedEventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private void OpenPanel()
        {
            ShowRootScreen();
            SetPanelOpen(true);
        }

        private void ClosePanel()
        {
            SetPanelOpen(false);
        }

        private void ShowRootScreen()
        {
            ActivateScreen(_rootScreen, false);
        }

        private void ShowAccountScreen()
        {
            ActivateScreen(_accountScreen, true);
        }

        private void ShowGameplayScreen()
        {
            ActivateScreen(_gameplayScreen, true);
        }

        private void ActivateScreen(IDevToolsScreen screen, bool isFeature)
        {
            _rootScreen?.Hide();
            _accountScreen?.Hide();
            _gameplayScreen?.Hide();
            _activeScreen = screen;
            _isFeatureScreenOpen = isFeature;
            _backButtonObject?.SetActive(isFeature);
            _activeScreen?.Show();
            ApplyLayout();
        }

        private void NavigateBack()
        {
            _activeScreen?.GoBack();
        }

        private void SetTitle(string title)
        {
            _titleText.text = string.IsNullOrWhiteSpace(title) ? "Developer" : title;
        }

        private void SetPanelOpen(bool isOpen)
        {
            _isPanelOpen = isOpen;
            _openButtonObject.SetActive(!isOpen);
            _panelObject.SetActive(isOpen);
        }

        private void ApplyLayout()
        {
            float scale = GetScale();
            float margin = _baseMargin * scale;
            Rect safeArea = GetSafeArea();

            // DEV launcher остаётся в штатной позиции независимо от пользовательского layout окна.
            float defaultLeft = safeArea.xMin + margin;
            float defaultTop = Mathf.Max(margin, Screen.height - safeArea.yMax + margin);
            DevToolsUiFactory.SetTopLeft(
                _openButtonRect,
                defaultLeft,
                defaultTop,
                _baseOpenButtonWidth * scale,
                _baseHeaderHeight * scale);

            float availableWidth = Mathf.Max(
                _baseOpenButtonWidth * scale,
                safeArea.width - margin * 2f);
            float availableHeight = Mathf.Max(_baseHeaderHeight * scale, safeArea.height - margin * 2f);
            float baseWidth = _isFeatureScreenOpen ? _baseFeaturePanelWidth : _baseRootPanelWidth;
            float baseHeight = _isFeatureScreenOpen ? _baseFeaturePanelHeight : _baseRootPanelHeight;
            Vector2 defaultSize = new Vector2(
                Mathf.Min(baseWidth * scale, availableWidth),
                Mathf.Min(baseHeight * scale, availableHeight));

            // До первого resize сохраняем прежние root/feature размеры, затем используем пользовательский размер.
            if (!_hasPanelLayout)
            {
                _panelTopLeft = new Vector2(defaultLeft, defaultTop);
                _panelSize = defaultSize;
                _hasPanelLayout = true;
            }
            else if (!_hasUserPanelSize && _layoutWasFeatureScreen != _isFeatureScreenOpen)
            {
                _panelSize = defaultSize;
            }

            _layoutWasFeatureScreen = _isFeatureScreenOpen;
            ClampPanelLayout(safeArea, margin, scale);

            float left = _panelTopLeft.x;
            float top = _panelTopLeft.y;
            float panelWidth = _panelSize.x;
            float panelHeight = _panelSize.y;
            DevToolsUiFactory.SetTopLeft(_panelRect, left, top, panelWidth, panelHeight);

            // Header и drag/resize handles перестраиваются вместе с окном.
            float inset = _baseInset * scale;
            float rowHeight = _baseHeaderHeight * scale;
            float resizeHandleSize = _baseResizeHandleSize * scale;
            float titleY = inset * 0.65f;
            float backWidth = _baseBackButtonWidth * scale;
            float titleLeft = _isFeatureScreenOpen ? inset + backWidth + inset * 0.5f : inset;
            DevToolsUiFactory.SetTopLeft(_backButtonRect, inset, titleY, backWidth, rowHeight);
            DevToolsUiFactory.SetTopLeft(
                _titleRect,
                titleLeft,
                titleY,
                panelWidth - titleLeft - inset - rowHeight,
                rowHeight);
            DevToolsUiFactory.SetTopLeft(
                _closeButtonRect,
                panelWidth - inset - rowHeight,
                titleY,
                rowHeight,
                rowHeight);
            DevToolsUiFactory.SetTopLeft(
                _dragHandleRect,
                0f,
                0f,
                panelWidth,
                titleY + rowHeight + inset * 0.35f);
            DevToolsUiFactory.SetTopLeft(
                _resizeHandleRect,
                panelWidth - resizeHandleSize,
                panelHeight - resizeHandleSize,
                resizeHandleSize,
                resizeHandleSize);

            // Active screen получает responsive viewport и не перекрывает touch resize handle.
            float contentTop = titleY + rowHeight + inset;
            float contentRight = Mathf.Max(inset, resizeHandleSize * 0.75f);
            float contentBottom = Mathf.Max(inset, resizeHandleSize * 0.75f);
            _activeScreen?.ApplyLayout(inset, contentTop, contentRight, contentBottom);
            _titleText.fontSize = Mathf.RoundToInt(16f * scale);
            foreach (Text text in _openButtonObject.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = Mathf.RoundToInt(DevToolsTheme.ButtonFontSize * scale);
            }
        }

        private void MovePanel(Vector2 pointerDelta)
        {
            if (!_hasPanelLayout)
                return;

            float scale = GetScale();
            _panelTopLeft += new Vector2(pointerDelta.x, -pointerDelta.y);
            ClampPanelLayout(GetSafeArea(), _baseMargin * scale, scale);
            ApplyLayout();
        }

        private void ResizePanel(Vector2 pointerDelta)
        {
            if (!_hasPanelLayout)
                return;

            float scale = GetScale();
            float margin = _baseMargin * scale;
            Rect safeArea = GetSafeArea();
            float safeRight = safeArea.xMax - margin;
            float safeBottom = Screen.height - safeArea.yMin - margin;
            float minimumWidth = Mathf.Min(
                _baseMinimumPanelWidth * scale,
                safeRight - _panelTopLeft.x);
            float minimumHeight = Mathf.Min(
                _baseMinimumPanelHeight * scale,
                safeBottom - _panelTopLeft.y);

            _panelSize = new Vector2(
                Mathf.Clamp(
                    _panelSize.x + pointerDelta.x,
                    minimumWidth,
                    safeRight - _panelTopLeft.x),
                Mathf.Clamp(
                    _panelSize.y - pointerDelta.y,
                    minimumHeight,
                    safeBottom - _panelTopLeft.y));
            _hasUserPanelSize = true;
            ApplyLayout();
        }

        private void ClampPanelLayout(Rect safeArea, float margin, float scale)
        {
            float safeLeft = safeArea.xMin + margin;
            float safeTop = Mathf.Max(margin, Screen.height - safeArea.yMax + margin);
            float safeRight = safeArea.xMax - margin;
            float safeBottom = Screen.height - safeArea.yMin - margin;
            float availableWidth = Mathf.Max(1f, safeRight - safeLeft);
            float availableHeight = Mathf.Max(1f, safeBottom - safeTop);
            float minimumWidth = Mathf.Min(_baseMinimumPanelWidth * scale, availableWidth);
            float minimumHeight = Mathf.Min(_baseMinimumPanelHeight * scale, availableHeight);

            _panelSize = new Vector2(
                Mathf.Clamp(_panelSize.x, minimumWidth, availableWidth),
                Mathf.Clamp(_panelSize.y, minimumHeight, availableHeight));
            _panelTopLeft = new Vector2(
                Mathf.Clamp(_panelTopLeft.x, safeLeft, safeRight - _panelSize.x),
                Mathf.Clamp(_panelTopLeft.y, safeTop, safeBottom - _panelSize.y));
        }

        private static Font LoadDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static float GetScale()
        {
            float widthScale = Screen.width / 720f;
            float heightScale = Screen.height / 360f;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, 1.6f);
        }

        private static Rect GetSafeArea()
        {
            Rect safeArea = Screen.safeArea;
            return safeArea.width > 0f && safeArea.height > 0f
                ? safeArea
                : new Rect(0f, 0f, Mathf.Max(Screen.width, 1f), Mathf.Max(Screen.height, 1f));
        }
    }
}
#endif
