#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.DevTools.Account;
using Assets.Scripts.DevTools.Core;
using Assets.Scripts.DevTools.Gameplay;
using Assets.Scripts.DevTools.Root;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Создаёт общий DEV-shell и размещает в нём независимые feature-экраны.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private const string _hostObjectName = "[DevToolsMenu]";
        private const int _sortingOrder = 32767;
        private const float _baseMargin = 10f;
        private const float _baseOpenButtonWidth = 64f;
        private const float _baseHeaderHeight = 40f;
        private const float _baseRootPanelWidth = 300f;
        private const float _baseRootPanelHeight = 220f;
        private const float _baseFeaturePanelWidth = 540f;
        private const float _baseFeaturePanelHeight = 569f;
        private const float _baseInset = 16f;
        private const float _baseBackButtonWidth = 88f;

        private static readonly Color _openButtonColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color _panelColor = new Color(1f, 1f, 1f, 0.985f);
        private static DevToolsMenuOverlay _instance;

        private GameObject _ownedEventSystemObject;
        private GameObject _openButtonObject;
        private RectTransform _openButtonRect;
        private GameObject _panelObject;
        private RectTransform _panelRect;
        private Text _titleText;
        private RectTransform _titleRect;
        private GameObject _backButtonObject;
        private RectTransform _backButtonRect;
        private RectTransform _closeButtonRect;
        private RootDevToolsScreen _rootScreen;
        private AccountDevToolsScreen _accountScreen;
        private GameplayDevToolsScreen _gameplayScreen;
        private IDevToolsScreen _activeScreen;
        private bool _isPanelOpen;
        private bool _isFeatureScreenOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject host = GameObject.Find(_hostObjectName) ?? new GameObject(_hostObjectName);
            _instance = host.GetComponent<DevToolsMenuOverlay>() ?? host.AddComponent<DevToolsMenuOverlay>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            EnsureUi();
            SetPanelOpen(_isPanelOpen);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            EnsureEventSystem();
            EnsureUi();
            ApplyLayout();
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
                    Destroy(_ownedEventSystemObject);
                    _ownedEventSystemObject = null;
                }

                return;
            }

            if (EventSystem.current != null)
            {
                return;
            }

            _ownedEventSystemObject = new GameObject("[DevToolsEventSystem]", typeof(EventSystem));
            _ownedEventSystemObject.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            _ownedEventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            _ownedEventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private void EnsureUi()
        {
            if (_panelObject != null)
            {
                return;
            }

            Font font = LoadDefaultFont();
            Canvas canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            DevToolsUiFactory ui = new DevToolsUiFactory(font);
            Button openButton = ui.CreateButton("OpenButton", transform, "DEV", _openButtonColor, OpenPanel, _baseHeaderHeight);
            _openButtonObject = openButton.gameObject;
            _openButtonRect = openButton.GetComponent<RectTransform>();

            _panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelObject.transform.SetParent(transform, false);
            _panelRect = _panelObject.GetComponent<RectTransform>();
            Image panelImage = _panelObject.GetComponent<Image>();
            panelImage.color = _panelColor;
            panelImage.raycastTarget = true;

            _titleText = ui.CreateText("Title", _panelObject.transform, "Developer", TextAnchor.MiddleLeft, FontStyle.Bold);
            _titleRect = _titleText.GetComponent<RectTransform>();
            Button backButton = ui.CreateButton("BackButton", _panelObject.transform, "Назад", Color.white, NavigateBack, _baseHeaderHeight);
            _backButtonObject = backButton.gameObject;
            _backButtonRect = backButton.GetComponent<RectTransform>();
            Button closeButton = ui.CreateButton("CloseButton", _panelObject.transform, "X", Color.white, ClosePanel, _baseHeaderHeight);
            _closeButtonRect = closeButton.GetComponent<RectTransform>();

            _rootScreen = new RootDevToolsScreen(
                _panelObject.transform,
                font,
                ClosePanel,
                ShowAccountScreen,
                ShowGameplayScreen,
                SetPanelTitle);
            _accountScreen = new AccountDevToolsScreen(_panelObject.transform, font, ShowRootScreen, SetPanelTitle);
            _gameplayScreen = new GameplayDevToolsScreen(
                _panelObject.transform,
                font,
                ClosePanel,
                ShowRootScreen,
                SetPanelTitle);
            ShowRootScreen();
            ApplyLayout();
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

        private void SetPanelTitle(string title)
        {
            if (_titleText != null)
            {
                _titleText.text = string.IsNullOrWhiteSpace(title) ? "Developer" : title;
            }
        }

        private void SetPanelOpen(bool isOpen)
        {
            _isPanelOpen = isOpen;
            _openButtonObject?.SetActive(!isOpen);
            _panelObject?.SetActive(isOpen);
        }

        private void ApplyLayout()
        {
            if (_openButtonRect == null || _panelRect == null)
            {
                return;
            }

            float scale = GetScale();
            float margin = _baseMargin * scale;
            Rect safeArea = GetSafeArea();
            float left = safeArea.xMin + margin;
            float top = Mathf.Max(margin, Screen.height - safeArea.yMax + margin);
            DevToolsUiFactory.SetTopLeft(
                _openButtonRect,
                left,
                top,
                _baseOpenButtonWidth * scale,
                _baseHeaderHeight * scale);

            float availableWidth = Mathf.Max(_baseOpenButtonWidth * scale, safeArea.xMax - left - margin);
            float availableHeight = Mathf.Max(_baseHeaderHeight * scale, safeArea.height - margin * 2f);
            float baseWidth = _isFeatureScreenOpen ? _baseFeaturePanelWidth : _baseRootPanelWidth;
            float baseHeight = _isFeatureScreenOpen ? _baseFeaturePanelHeight : _baseRootPanelHeight;
            float panelWidth = Mathf.Min(baseWidth * scale, availableWidth);
            float panelHeight = Mathf.Min(baseHeight * scale, availableHeight);
            DevToolsUiFactory.SetTopLeft(_panelRect, left, top, panelWidth, panelHeight);

            float inset = _baseInset * scale;
            float rowHeight = _baseHeaderHeight * scale;
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

            float contentTop = titleY + rowHeight + inset;
            _activeScreen?.ApplyLayout(inset, contentTop, inset, inset);
            _titleText.fontSize = Mathf.RoundToInt(16f * scale);
            foreach (Text text in _openButtonObject.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = Mathf.RoundToInt(DevToolsTheme.ButtonFontSize * scale);
            }
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
