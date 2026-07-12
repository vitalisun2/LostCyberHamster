#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Bot;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Создаёт постоянный оверлей DEV-меню для Editor и development builds.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private const string _hostObjectName = "[DevToolsMenu]";
        private const string _openButtonObjectName = "OpenButton";
        private const string _panelObjectName = "Panel";
        private const string _rootScreenObjectName = "RootScreen";
        private const string _backButtonObjectName = "BackButton";
        private const string _closeButtonObjectName = "CloseButton";
        private const string _botButtonObjectName = "BotButton";
        private const string _unlockAllButtonObjectName = "UnlockAllButton";
        private const string _completeLevelButtonObjectName = "CompleteLevelButton";
        private const string _resetProgressButtonObjectName = "ResetProgressButton";
        private const string _accountButtonObjectName = "AccountButton";
        private const string _statusTextObjectName = "StatusText";

        private const int _sortingOrder = 32767;
        private const float _baseMargin = 10f;
        private const float _baseOpenButtonWidth = 64f;
        private const float _baseButtonHeight = 40f;
        private const float _baseRootPanelWidth = 260f;
        private const float _baseRootPanelHeight = 310f;
        private const float _baseAccountPanelWidth = 540f;
        private const float _baseAccountPanelHeight = 569f;

        private static readonly Color _openButtonColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color _panelColor = new Color(1f, 1f, 1f, 0.985f);
        private static readonly Color _enabledColor = new Color(0.78f, 1f, 0.82f, 1f);
        private static readonly Color _disabledColor = new Color(1f, 0.82f, 0.78f, 1f);

        private static DevToolsMenuOverlay _instance;

        private bool _isPanelOpen;
        private Font _font;
        private GameObject _ownedEventSystemObject;

        private GameObject _openButtonObject;
        private RectTransform _openButtonRect;

        private GameObject _panelObject;
        private RectTransform _panelRect;
        private GameObject _rootScreenObject;
        private Text _titleText;
        private RectTransform _titleRect;
        private GameObject _backButtonObject;
        private RectTransform _backButtonRect;
        private RectTransform _closeButtonRect;
        private RectTransform _botButtonRect;
        private RectTransform _unlockAllButtonRect;
        private RectTransform _completeLevelButtonRect;
        private RectTransform _resetProgressButtonRect;
        private RectTransform _accountButtonRect;
        private RectTransform _statusTextRect;

        private Button _botButton;
        private Image _botButtonImage;
        private Text _botButtonText;

        private Image _unlockAllButtonImage;
        private Text _unlockAllButtonText;

        private Button _completeLevelButton;

        private Text _statusText;
        private AccountDevToolsScreen _accountScreen;
        private bool _isAccountScreenOpen;

        /// <summary>
        /// Creates the persistent developer menu host before user scenes are loaded.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
                return;

            GameObject host = GameObject.Find(_hostObjectName);
            if (host == null)
                host = new GameObject(_hostObjectName);

            _instance = host.GetComponent<DevToolsMenuOverlay>();
            if (_instance == null)
                _instance = host.AddComponent<DevToolsMenuOverlay>();

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
                _instance = null;
        }

        private void Update()
        {
            EnsureEventSystem();
            EnsureUi();
            ApplyLayout();
            RefreshButtonState();
            if (_isAccountScreenOpen)
                _accountScreen?.RefreshPresentation();
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
                return;

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
                return;

            _font = LoadDefaultFont();

            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            Button openButton = CreateButton(
                _openButtonObjectName,
                transform,
                "DEV",
                _openButtonColor,
                OpenPanel);
            _openButtonObject = openButton.gameObject;
            _openButtonRect = openButton.GetComponent<RectTransform>();

            CreatePanel();
            ApplyLayout();
            RefreshButtonState();
        }

        private void CreatePanel()
        {
            _panelObject = new GameObject(_panelObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelObject.transform.SetParent(transform, false);

            _panelRect = _panelObject.GetComponent<RectTransform>();
            Image panelImage = _panelObject.GetComponent<Image>();
            panelImage.color = _panelColor;
            panelImage.raycastTarget = true;

            _titleText = CreateText("Title", _panelObject.transform, "Developer", TextAnchor.MiddleLeft, FontStyle.Bold);
            _titleRect = _titleText.GetComponent<RectTransform>();

            Button backButton = CreateButton(
                _backButtonObjectName,
                _panelObject.transform,
                "Назад",
                Color.white,
                NavigateBack);
            _backButtonObject = backButton.gameObject;
            _backButtonRect = backButton.GetComponent<RectTransform>();

            Button closeButton = CreateButton(
                _closeButtonObjectName,
                _panelObject.transform,
                "X",
                Color.white,
                ClosePanel);
            _closeButtonRect = closeButton.GetComponent<RectTransform>();

            _rootScreenObject = new GameObject(_rootScreenObjectName, typeof(RectTransform));
            _rootScreenObject.transform.SetParent(_panelObject.transform, false);
            RectTransform rootScreenRect = _rootScreenObject.GetComponent<RectTransform>();
            rootScreenRect.anchorMin = Vector2.zero;
            rootScreenRect.anchorMax = Vector2.one;
            rootScreenRect.offsetMin = Vector2.zero;
            rootScreenRect.offsetMax = Vector2.zero;

            _botButton = CreateButton(
                _botButtonObjectName,
                _rootScreenObject.transform,
                "Bot Off",
                _disabledColor,
                ToggleBot);
            _botButtonRect = _botButton.GetComponent<RectTransform>();
            _botButtonImage = _botButton.GetComponent<Image>();
            _botButtonText = _botButton.GetComponentInChildren<Text>();

            Button unlockAllButton = CreateButton(
                _unlockAllButtonObjectName,
                _rootScreenObject.transform,
                "Unlock All Off",
                _disabledColor,
                ToggleUnlockAll);
            _unlockAllButtonRect = unlockAllButton.GetComponent<RectTransform>();
            _unlockAllButtonImage = unlockAllButton.GetComponent<Image>();
            _unlockAllButtonText = unlockAllButton.GetComponentInChildren<Text>();

            _completeLevelButton = CreateButton(
                _completeLevelButtonObjectName,
                _rootScreenObject.transform,
                "Complete Level (3 Stars)",
                Color.white,
                CompleteLevelWithThreeStars);
            _completeLevelButtonRect = _completeLevelButton.GetComponent<RectTransform>();

            Button resetProgressButton = CreateButton(
                _resetProgressButtonObjectName,
                _rootScreenObject.transform,
                "Reset Progress",
                Color.white,
                ResetProgress);
            _resetProgressButtonRect = resetProgressButton.GetComponent<RectTransform>();

            Button accountButton = CreateButton(
                _accountButtonObjectName,
                _rootScreenObject.transform,
                "Аккаунт",
                Color.white,
                ShowAccountScreen);
            _accountButtonRect = accountButton.GetComponent<RectTransform>();

            _statusText = CreateText(_statusTextObjectName, _rootScreenObject.transform, "Bot is not ready", TextAnchor.MiddleLeft);
            _statusTextRect = _statusText.GetComponent<RectTransform>();

            _accountScreen = new AccountDevToolsScreen(
                _panelObject.transform,
                _font,
                ShowRootScreen,
                SetPanelTitle);
            ShowRootScreen();
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Color color,
            UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.62f);
            button.colors = colors;

            Text text = CreateText("Text", buttonObject.transform, label, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private Text CreateText(
            string objectName,
            Transform parent,
            string label,
            TextAnchor alignment,
            FontStyle fontStyle = FontStyle.Normal)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = 15;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.black;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            return text;
        }

        private static Font LoadDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static float GetScale()
        {
            float widthScale = Screen.width / 720f;
            float heightScale = Screen.height / 360f;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, 1.6f);
        }

        private void ApplyLayout()
        {
            float scale = GetScale();
            float margin = _baseMargin * scale;
            Rect safeArea = GetSafeArea();
            float left = safeArea.xMin + margin;
            float top = Mathf.Max(margin, Screen.height - safeArea.yMax + margin);

            SetTopLeft(
                _openButtonRect,
                left,
                top,
                _baseOpenButtonWidth * scale,
                _baseButtonHeight * scale);

            float availableWidth = Mathf.Max(_baseOpenButtonWidth * scale, safeArea.xMax - left - margin);
            float availableHeight = Mathf.Max(_baseButtonHeight * scale, safeArea.height - margin * 2f);
            float basePanelWidth = _isAccountScreenOpen ? _baseAccountPanelWidth : _baseRootPanelWidth;
            float basePanelHeight = _isAccountScreenOpen ? _baseAccountPanelHeight : _baseRootPanelHeight;
            float panelWidth = Mathf.Min(basePanelWidth * scale, availableWidth);
            float panelHeight = Mathf.Min(basePanelHeight * scale, availableHeight);
            SetTopLeft(_panelRect, left, top, panelWidth, panelHeight);

            float inset = 16f * scale;
            float rowHeight = _baseButtonHeight * scale;
            float titleY = inset * 0.65f;

            float backButtonWidth = 88f * scale;
            float titleLeft = _isAccountScreenOpen ? inset + backButtonWidth + inset * 0.5f : inset;
            SetTopLeft(_backButtonRect, inset, titleY, backButtonWidth, rowHeight);
            SetTopLeft(_titleRect, titleLeft, titleY, panelWidth - titleLeft - inset - rowHeight, rowHeight);
            SetTopLeft(_closeButtonRect, panelWidth - inset - rowHeight, titleY, rowHeight, rowHeight);
            SetTopLeft(_botButtonRect, inset, titleY + rowHeight + inset, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_unlockAllButtonRect, inset, titleY + rowHeight * 2f + inset * 1.75f, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_completeLevelButtonRect, inset, titleY + rowHeight * 3f + inset * 2.5f, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_resetProgressButtonRect, inset, titleY + rowHeight * 4f + inset * 3.25f, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_accountButtonRect, inset, titleY + rowHeight * 5f + inset * 4f, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_statusTextRect, inset, titleY + rowHeight * 6f + inset * 4.45f, panelWidth - inset * 2f, rowHeight);

            float contentTop = titleY + rowHeight + inset;
            _accountScreen?.ApplyLayout(inset, contentTop, inset, inset);

            int buttonFontSize = Mathf.RoundToInt(15f * scale);
            int titleFontSize = Mathf.RoundToInt(16f * scale);
            SetTextFontSize(_openButtonObject, buttonFontSize);
            if (_titleText != null)
                _titleText.fontSize = titleFontSize;
        }

        private static Rect GetSafeArea()
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width > 0f && safeArea.height > 0f)
                return safeArea;

            return new Rect(0f, 0f, Mathf.Max(Screen.width, 1f), Mathf.Max(Screen.height, 1f));
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(Mathf.Max(width, 1f), Mathf.Max(height, 1f));
        }

        private static void SetTextFontSize(GameObject root, int fontSize)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                text.fontSize = fontSize;
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

        private void ShowAccountScreen()
        {
            _isAccountScreenOpen = true;
            _rootScreenObject?.SetActive(false);
            _backButtonObject?.SetActive(true);
            _accountScreen?.Show();
            ApplyLayout();
        }

        private void ShowRootScreen()
        {
            _isAccountScreenOpen = false;
            _accountScreen?.Hide();
            _rootScreenObject?.SetActive(true);
            _backButtonObject?.SetActive(false);
            SetPanelTitle("Developer");
            ApplyLayout();
        }

        private void NavigateBack()
        {
            _accountScreen?.GoBack();
        }

        private void SetPanelTitle(string title)
        {
            if (_titleText != null)
                _titleText.text = string.IsNullOrWhiteSpace(title) ? "Аккаунт" : title;
        }

        private void SetPanelOpen(bool isOpen)
        {
            _isPanelOpen = isOpen;
            if (_openButtonObject != null)
                _openButtonObject.SetActive(!isOpen);

            if (_panelObject != null)
                _panelObject.SetActive(isOpen);

            RefreshButtonState();
        }

        private void ToggleBot()
        {
            RuntimeBotController bot = FindBot();
            if (bot != null)
                bot.SetEnabled(!bot.IsEnabled);

            RefreshButtonState();
        }

        private void ToggleUnlockAll()
        {
            DevToolsRuntimeState.UnlockAllLevels = !DevToolsRuntimeState.UnlockAllLevels;
            RefreshButtonState();
        }

        private void ResetProgress()
        {
            GameDataManager.ResetLocalData();
            DevToolsRuntimeState.UnlockAllLevels = false;
            UIManager.OnRepaintScreen?.Invoke();
            RefreshButtonState();
        }

        private void CompleteLevelWithThreeStars()
        {
            LevelController levelController = LevelController.Instance;
            if (levelController?.LevelData?.GameManager?.State != GameState.PLAYING)
                return;

            Hamster hamster = Object.FindAnyObjectByType<Hamster>(FindObjectsInactive.Include);
            if (hamster == null)
                return;

            hamster.Lives.Value = 3;
            ClosePanel();
            levelController.Finish();
        }

        private void RefreshButtonState()
        {
            if (_botButton == null)
                return;

            RuntimeBotController bot = FindBot();
            bool botAvailable = bot != null;
            bool botEnabled = botAvailable && bot.IsEnabled;

            _botButton.interactable = botAvailable;
            _botButtonText.text = botEnabled ? "Bot On" : "Bot Off";
            _botButtonImage.color = botEnabled ? _enabledColor : _disabledColor;

            bool unlockAllLevels = DevToolsRuntimeState.UnlockAllLevels;
            _unlockAllButtonText.text = unlockAllLevels ? "Unlock All On" : "Unlock All Off";
            _unlockAllButtonImage.color = unlockAllLevels ? _enabledColor : _disabledColor;

            if (_completeLevelButton != null)
            {
                _completeLevelButton.interactable =
                    LevelController.Instance?.LevelData?.GameManager?.State == GameState.PLAYING;
            }

            if (_statusText != null)
                _statusText.gameObject.SetActive(!botAvailable);
        }

        private static RuntimeBotController FindBot()
        {
            return Object.FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include);
        }
    }
}
#endif
