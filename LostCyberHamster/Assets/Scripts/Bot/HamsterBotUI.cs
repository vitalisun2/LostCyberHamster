using Assets.Scripts.Bot.Learning;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Легковесный OnGUI-оверлей для отображения состояния бота.
    /// Показывает режим, стиль, обучение. Не требует Canvas/UI Toolkit.
    /// </summary>
    public class HamsterBotUI : MonoBehaviour
    {
        private static readonly Color ColorPlay = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorTest = new Color(0.2f, 0.6f, 1f);
        private static readonly Color ColorAnalytics = new Color(1f, 0.6f, 0.2f);
        private static readonly Color ColorDisabled = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color ColorTraining = new Color(1f, 0.5f, 0f);
        private static readonly Color ColorImproved = new Color(0.3f, 1f, 0.3f);
        private static readonly Color ColorNoImprove = new Color(1f, 0.4f, 0.4f);

        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
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

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 2, 2),
                wordWrap = true
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

            float w = 340f;
            float lineHeight = 20f;
            float x = Screen.width - w - 10f;  // справа, с отступом 10px
            float y = 80f;                      // под кнопкой PAUSE

            if (!bot.IsEnabled)
            {
                _labelStyle.normal.textColor = ColorDisabled;
                GUI.Box(new Rect(x, y, w, lineHeight + 12f), GUIContent.none, _boxStyle);
                GUI.Label(new Rect(x, y, w, lineHeight + 12f),
                    "BOT: OFF  (F1 on, F2 mode, F3 style, F4 train)", _labelStyle);
                return;
            }

            Color modeColor = bot.CurrentMode switch
            {
                BotMode.Play => ColorPlay,
                BotMode.Test => ColorTest,
                BotMode.Analytics => ColorAnalytics,
                _ => Color.white
            };

            Color styleColor = bot.CurrentPlayStyle switch
            {
                BotPlayStyle.Survival => new Color(0.9f, 0.9f, 0.3f),
                BotPlayStyle.ThreeStars => new Color(1f, 0.85f, 0f),
                BotPlayStyle.BonusHunter => new Color(0.3f, 1f, 0.5f),
                BotPlayStyle.Perfectionist => new Color(0.6f, 0.4f, 1f),
                BotPlayStyle.UltaMaster => new Color(1f, 0.3f, 0.3f),
                BotPlayStyle.GodMode => new Color(1f, 0.2f, 0.8f),
                _ => Color.white
            };

            // Определяем высоту бокса
            var orch = bot.LearningOrchestrator;
            bool showTraining = orch != null && orch.IsTrainingMode;
            float boxHeight = showTraining
                ? lineHeight * 3 + 16f + lineHeight * 4 + 8f
                : lineHeight * 3 + 16f;

            GUI.Box(new Rect(x, y, w, boxHeight), GUIContent.none, _boxStyle);

            // Строка 1: Режим + хоткеи
            _labelStyle.normal.textColor = modeColor;
            GUI.Label(new Rect(x, y, w, lineHeight + 12f),
                $"BOT: {bot.CurrentMode}  |  F1 off  F2 mode  F3 style  F4 train", _labelStyle);

            // Строка 2: Стиль + обучение
            float line2Y = y + lineHeight + 2f;
            _labelStyle.fontSize = 13;
            _labelStyle.normal.textColor = styleColor;

            string trainingTag = bot.IsTrainingMode ? "  [TRAINING]" : "";
            GUI.Label(new Rect(x, line2Y, w, lineHeight + 12f),
                $"Style: {bot.CurrentPlayStyle}{trainingTag}", _labelStyle);

            // Строка 3+: Training info
            if (showTraining)
            {
                float trainY = line2Y + lineHeight + 6f;

                // Generation / Fitness
                _smallStyle.normal.textColor = ColorTraining;
                GUI.Label(new Rect(x, trainY, w, lineHeight),
                    $"Gen: {orch.CurrentGeneration}  |  Best: {orch.BestFitness:F0}  |  Last: {orch.LastFitness:F0}",
                    _smallStyle);
                trainY += lineHeight;

                // Improved?
                if (orch.LastFitness > 0)
                {
                    _smallStyle.normal.textColor = orch.LastSessionImproved ? ColorImproved : ColorNoImprove;
                    string tag = orch.LastSessionImproved ? "IMPROVED" : "no improvement";
                    GUI.Label(new Rect(x, trainY, w, lineHeight), $"  >> {tag}", _smallStyle);
                    trainY += lineHeight;
                }

                // Last mutation info (truncated)
                if (!string.IsNullOrEmpty(orch.LastMutationInfo))
                {
                    _smallStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                    string info = orch.LastMutationInfo;
                    if (info.Length > 60) info = info.Substring(0, 60) + "...";
                    GUI.Label(new Rect(x, trainY, w, lineHeight * 2), info, _smallStyle);
                }
            }

            // Сброс стиля
            _labelStyle.normal.textColor = Color.white;
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
