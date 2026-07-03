#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Bot;
using UnityEngine;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Показывает компактное developer-меню поверх игры в Editor и development build.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private const string _hostObjectName = "[DevToolsMenu]";
        private const float _baseMargin = 10f;
        private const float _baseOpenButtonWidth = 64f;
        private const float _baseButtonHeight = 34f;
        private const float _basePanelWidth = 260f;
        private const float _basePanelHeight = 178f;

        private static DevToolsMenuOverlay _instance;

        private bool _isPanelOpen;
        private int _styleFontSize;
        private GUIStyle _buttonStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
#if UNITY_EDITOR
        private int _editorMouseDownButtonId = -1;
#endif

        /// <summary>
        /// Создаёт persistent host developer-меню до загрузки пользовательских сцен.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
                return;

            // Переиспользует host, если он уже появился после reload-а скриптов.
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
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnGUI()
        {
            if (!IsAvailable())
                return;

            // Готовит IMGUI styles под текущий размер экрана.
            float scale = GetScale();
            EnsureStyles(scale);

            // Рисует поверх игрового UI и восстанавливает глобальное GUI-состояние.
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;

            GUI.depth = -1000;
            try
            {
                Draw(scale);
            }
            finally
            {
                GUI.depth = previousDepth;
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
            }
        }

        /// <summary>
        /// Подтверждает доступность меню в сборках, где dev-only overlay включён через compile-time guard.
        /// </summary>
        private static bool IsAvailable()
        {
            return true;
        }

        private static float GetScale()
        {
            float widthScale = Screen.width / 720f;
            float heightScale = Screen.height / 360f;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, 1.6f);
        }

        private void EnsureStyles(float scale)
        {
            int fontSize = Mathf.RoundToInt(14f * scale);
            if (_buttonStyle != null && _styleFontSize == fontSize)
                return;

            // Пересоздаёт styles только при изменении расчетного font size.
            _styleFontSize = fontSize;
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(
                    Mathf.RoundToInt(12f * scale),
                    Mathf.RoundToInt(12f * scale),
                    Mathf.RoundToInt(10f * scale),
                    Mathf.RoundToInt(10f * scale))
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            _titleStyle = new GUIStyle(_labelStyle)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold
            };
        }

        private void Draw(float scale)
        {
            Rect openButtonRect = GetOpenButtonRect(scale);

            // В закрытом состоянии оставляет только компактную кнопку входа.
            if (!_isPanelOpen)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.72f);
                if (DrawButton(1, openButtonRect, "DEV"))
                    _isPanelOpen = true;

                return;
            }

            DrawPanel(openButtonRect, scale);
        }

        private static Rect GetOpenButtonRect(float scale)
        {
            // Переводит верхний край safe area в координаты IMGUI.
            Rect safeArea = Screen.safeArea;
            float margin = _baseMargin * scale;
            float left = safeArea.xMin + margin;
            float top = Mathf.Max(margin, Screen.height - safeArea.yMax + margin);

            return new Rect(
                left,
                top,
                _baseOpenButtonWidth * scale,
                _baseButtonHeight * scale);
        }

        private void DrawPanel(Rect openButtonRect, float scale)
        {
            // Держит панель рядом с кнопкой и не выпускает её за правый край экрана.
            float margin = _baseMargin * scale;
            float panelWidth = Mathf.Min(_basePanelWidth * scale, Screen.width - openButtonRect.x - margin);
            Rect panelRect = new Rect(
                openButtonRect.x,
                openButtonRect.y,
                panelWidth,
                _basePanelHeight * scale);

            GUI.color = new Color(1f, 1f, 1f, 0.94f);
            GUI.Box(panelRect, GUIContent.none, _boxStyle);
            GUI.color = Color.white;

            float inset = 12f * scale;
            float rowHeight = _baseButtonHeight * scale;
            Rect titleRect = new Rect(
                panelRect.x + inset,
                panelRect.y + inset * 0.65f,
                panelRect.width - inset * 2f - rowHeight,
                rowHeight);
            Rect closeRect = new Rect(
                panelRect.xMax - inset - rowHeight,
                titleRect.y,
                rowHeight,
                rowHeight);

            GUI.Label(titleRect, "Developer", _titleStyle);
            if (DrawButton(2, closeRect, "X"))
                _isPanelOpen = false;

            // Считывает актуальное состояние runtime-бота каждый кадр меню.
            RuntimeBotController bot = FindBot();
            bool botAvailable = bot != null;
            bool botEnabled = botAvailable && bot.IsEnabled;

            Rect toggleRect = new Rect(
                panelRect.x + inset,
                titleRect.yMax + inset,
                panelRect.width - inset * 2f,
                rowHeight);

            GUI.enabled = botAvailable;
            GUI.color = botEnabled
                ? new Color(0.78f, 1f, 0.82f, 1f)
                : new Color(1f, 0.82f, 0.78f, 1f);

            if (DrawButton(3, toggleRect, botEnabled ? "Bot On" : "Bot Off"))
                bot.SetEnabled(!botEnabled);

            GUI.color = Color.white;
            GUI.enabled = true;

            Rect unlockAllRect = new Rect(
                panelRect.x + inset,
                toggleRect.yMax + inset * 0.75f,
                panelRect.width - inset * 2f,
                rowHeight);
            bool unlockAllLevels = DevToolsRuntimeState.UnlockAllLevels;
            GUI.color = unlockAllLevels
                ? new Color(0.78f, 1f, 0.82f, 1f)
                : new Color(1f, 0.82f, 0.78f, 1f);

            if (DrawButton(4, unlockAllRect, unlockAllLevels ? "Unlock All On" : "Unlock All Off"))
                DevToolsRuntimeState.UnlockAllLevels = !unlockAllLevels;

            GUI.color = Color.white;

            // Показывает статус ожидания, если bot controller ещё не создан.
            if (!botAvailable)
            {
                Rect statusRect = new Rect(
                    panelRect.x + inset,
                    unlockAllRect.yMax + inset * 0.45f,
                    panelRect.width - inset * 2f,
                    rowHeight);
                GUI.Label(statusRect, "Bot is not ready", _labelStyle);
            }
        }

        private static RuntimeBotController FindBot()
        {
            return Object.FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include);
        }

        private bool DrawButton(int buttonId, Rect rect, string text)
        {
#if UNITY_EDITOR
            bool editorMouseClicked = GUI.enabled && TryHandleEditorMouseClick(buttonId, rect);
#else
            bool editorMouseClicked = false;
#endif
            return GUI.Button(rect, text, _buttonStyle) || editorMouseClicked;
        }

#if UNITY_EDITOR
        private bool TryHandleEditorMouseClick(int buttonId, Rect rect)
        {
            Event current = Event.current;
            if (current == null || current.button != 0)
                return false;

            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                _editorMouseDownButtonId = buttonId;
                current.Use();
                return false;
            }

            if (current.type != EventType.MouseUp || _editorMouseDownButtonId != buttonId)
                return false;

            _editorMouseDownButtonId = -1;
            if (!rect.Contains(current.mousePosition))
                return false;

            current.Use();
            return true;
        }
#endif
    }
}
#endif
