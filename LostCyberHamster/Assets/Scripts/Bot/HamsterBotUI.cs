using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Легковесный OnGUI-оверлей для отображения состояния бота.
    /// Показывает режим, статус, последнее действие. Не требует Canvas/UI Toolkit.
    /// </summary>
    public class HamsterBotUI : MonoBehaviour
    {
        private static readonly Color ColorPlay = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorTest = new Color(0.2f, 0.6f, 1f);
        private static readonly Color ColorAnalytics = new Color(1f, 0.6f, 0.2f);
        private static readonly Color ColorDisabled = new Color(0.5f, 0.5f, 0.5f);

        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 4, 4)
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f)) },
                padding = new RectOffset(8, 8, 6, 6)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            var bot = HamsterBot.Instance;
            if (bot == null) return;

            InitStyles();

            float x = 10f;
            float y = 10f;
            float w = 260f;
            float lineHeight = 20f;

            if (!bot.IsEnabled)
            {
                _labelStyle.normal.textColor = ColorDisabled;
                GUI.Box(new Rect(x, y, w, lineHeight + 12f), GUIContent.none, _boxStyle);
                GUI.Label(new Rect(x, y, w, lineHeight + 12f), "BOT: OFF  (F1 toggle, F2 mode)", _labelStyle);
                return;
            }

            Color modeColor = bot.CurrentMode switch
            {
                BotMode.Play => ColorPlay,
                BotMode.Test => ColorTest,
                BotMode.Analytics => ColorAnalytics,
                _ => Color.white
            };

            float boxHeight = lineHeight * 2 + 16f;
            GUI.Box(new Rect(x, y, w, boxHeight), GUIContent.none, _boxStyle);

            _labelStyle.normal.textColor = modeColor;
            GUI.Label(new Rect(x, y, w, lineHeight + 12f),
                $"BOT: {bot.CurrentMode}  |  F1 off  F2 mode", _labelStyle);

            _labelStyle.normal.textColor = Color.white;
            _labelStyle.fontSize = 12;
            _labelStyle.fontStyle = FontStyle.Normal;
            // No direct access to _lastDecisionText — we show mode only
            // (BotLogger handles detailed output to file)
            _labelStyle.fontSize = 14;
            _labelStyle.fontStyle = FontStyle.Bold;
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            var result = new Texture2D(width, height, TextureFormat.ARGB32, false);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
