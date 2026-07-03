#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Bot;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Shows a compact developer menu over the game in Editor and development builds.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private const string _hostObjectName = "[DevToolsMenu]";
        private const string _openButtonObjectName = "OpenButton";
        private const string _panelObjectName = "Panel";
        private const string _closeButtonObjectName = "CloseButton";
        private const string _botButtonObjectName = "BotButton";
        private const string _unlockAllButtonObjectName = "UnlockAllButton";
        private const string _statusTextObjectName = "StatusText";

        private const int _sortingOrder = 32767;
        private const float _baseMargin = 10f;
        private const float _baseOpenButtonWidth = 64f;
        private const float _baseButtonHeight = 34f;
        private const float _basePanelWidth = 260f;
        private const float _basePanelHeight = 178f;

        private static readonly Color _openButtonColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color _panelColor = new Color(1f, 1f, 1f, 0.94f);
        private static readonly Color _enabledColor = new Color(0.78f, 1f, 0.82f, 1f);
        private static readonly Color _disabledColor = new Color(1f, 0.82f, 0.78f, 1f);

        private static DevToolsMenuOverlay _instance;

        private bool _isPanelOpen;
        private Font _font;

        private GameObject _openButtonObject;
        private RectTransform _openButtonRect;

        private GameObject _panelObject;
        private RectTransform _panelRect;
        private RectTransform _titleRect;
        private RectTransform _closeButtonRect;
        private RectTransform _botButtonRect;
        private RectTransform _unlockAllButtonRect;
        private RectTransform _statusTextRect;

        private Button _botButton;
        private Image _botButtonImage;
        private Text _botButtonText;

        private Image _unlockAllButtonImage;
        private Text _unlockAllButtonText;

        private Text _statusText;

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
            EnsureUi();
            ApplyLayout();
            RefreshButtonState();
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

            Text titleText = CreateText("Title", _panelObject.transform, "Developer", TextAnchor.MiddleLeft, FontStyle.Bold);
            _titleRect = titleText.GetComponent<RectTransform>();

            Button closeButton = CreateButton(
                _closeButtonObjectName,
                _panelObject.transform,
                "X",
                Color.white,
                ClosePanel);
            _closeButtonRect = closeButton.GetComponent<RectTransform>();

            _botButton = CreateButton(
                _botButtonObjectName,
                _panelObject.transform,
                "Bot Off",
                _disabledColor,
                ToggleBot);
            _botButtonRect = _botButton.GetComponent<RectTransform>();
            _botButtonImage = _botButton.GetComponent<Image>();
            _botButtonText = _botButton.GetComponentInChildren<Text>();

            Button unlockAllButton = CreateButton(
                _unlockAllButtonObjectName,
                _panelObject.transform,
                "Unlock All Off",
                _disabledColor,
                ToggleUnlockAll);
            _unlockAllButtonRect = unlockAllButton.GetComponent<RectTransform>();
            _unlockAllButtonImage = unlockAllButton.GetComponent<Image>();
            _unlockAllButtonText = unlockAllButton.GetComponentInChildren<Text>();

            _statusText = CreateText(_statusTextObjectName, _panelObject.transform, "Bot is not ready", TextAnchor.MiddleLeft);
            _statusTextRect = _statusText.GetComponent<RectTransform>();
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
            text.fontSize = 14;
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

            float availableWidth = Mathf.Max(_baseOpenButtonWidth * scale, Screen.width - left - margin);
            float panelWidth = Mathf.Min(_basePanelWidth * scale, availableWidth);
            SetTopLeft(_panelRect, left, top, panelWidth, _basePanelHeight * scale);

            float inset = 12f * scale;
            float rowHeight = _baseButtonHeight * scale;
            float titleY = inset * 0.65f;

            SetTopLeft(_titleRect, inset, titleY, panelWidth - inset * 2f - rowHeight, rowHeight);
            SetTopLeft(_closeButtonRect, panelWidth - inset - rowHeight, titleY, rowHeight, rowHeight);
            SetTopLeft(_botButtonRect, inset, titleY + rowHeight + inset, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_unlockAllButtonRect, inset, titleY + rowHeight * 2f + inset * 1.75f, panelWidth - inset * 2f, rowHeight);
            SetTopLeft(_statusTextRect, inset, titleY + rowHeight * 3f + inset * 2.2f, panelWidth - inset * 2f, rowHeight);

            int buttonFontSize = Mathf.RoundToInt(14f * scale);
            int titleFontSize = Mathf.RoundToInt(16f * scale);
            SetTextFontSize(_openButtonObject, buttonFontSize);
            SetTextFontSize(_panelObject, buttonFontSize);
            Text titleText = _titleRect.GetComponent<Text>();
            if (titleText != null)
                titleText.fontSize = titleFontSize;
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
            SetPanelOpen(true);
        }

        private void ClosePanel()
        {
            SetPanelOpen(false);
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
