using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Легковесный OnGUI-оверлей: BOT ON/OFF (F1).
    /// </summary>
    public class HamsterBotUI : MonoBehaviour
    {
        private static readonly Color ColorOn = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorOff = new Color(0.5f, 0.5f, 0.5f);

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

            float w = 220f;
            float h = 32f;
            float x = Screen.width - w - 10f;
            float y = 80f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);

            if (bot.IsEnabled)
            {
                _labelStyle.normal.textColor = ColorOn;
                GUI.Label(new Rect(x, y, w, h), "BOT: ON  (F1 off)", _labelStyle);
            }
            else
            {
                _labelStyle.normal.textColor = ColorOff;
                GUI.Label(new Rect(x, y, w, h), "BOT: OFF  (F1 on)", _labelStyle);
            }
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
