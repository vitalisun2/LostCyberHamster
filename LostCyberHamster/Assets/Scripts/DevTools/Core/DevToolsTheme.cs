#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Хранит единые визуальные токены runtime DEV-интерфейса.
    /// </summary>
    internal static class DevToolsTheme
    {
        public const float UiScale = 2f;
        public const float MaxScreenScale = 1.8f;
        public const float ContentHorizontalPadding = 28f * UiScale;
        public const float PortraitContentWidth = 820f;
        public const float LandscapeContentWidth = 1320f;
        public const float ContentSpacing = 8f * UiScale;
        public const float ButtonHeight = 38f * UiScale;
        public const float PrimaryButtonHeight = 40f * UiScale;
        public const int BodyFontSize = 32;
        public const int ButtonFontSize = 36;
        public const int HeadingFontSize = 38;
        public const int WindowTitleFontSize = 40;

        public static readonly Color Button = Color.white;
        public static readonly Color Primary = new Color(0.48f, 0.82f, 1f, 1f);
        public static readonly Color Navigation = new Color(0.86f, 0.93f, 1f, 1f);
        public static readonly Color StatusCard = new Color(0.93f, 0.96f, 1f, 1f);
        public static readonly Color Surface = new Color(0.97f, 0.98f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.78f, 0.74f, 1f);
        public static readonly Color DangerCard = new Color(1f, 0.92f, 0.91f, 1f);
        public static readonly Color Enabled = new Color(0.78f, 1f, 0.82f, 1f);
        public static readonly Color Disabled = new Color(1f, 0.82f, 0.78f, 1f);

        public static float GetScreenScale()
        {
            float widthScale = Screen.width / 720f;
            float heightScale = Screen.height / 360f;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, MaxScreenScale);
        }

        public static float ScaleSize(float value) => value * GetScreenScale();

        public static int ScaleFont(int value) => Mathf.RoundToInt(value * GetScreenScale());
    }
}
#endif
