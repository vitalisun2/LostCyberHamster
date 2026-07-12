#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Хранит единые визуальные токены runtime DEV-интерфейса.
    /// </summary>
    internal static class DevToolsTheme
    {
        public const float ContentSpacing = 8f;
        public const float ButtonHeight = 38f;
        public const float PrimaryButtonHeight = 40f;
        public const int BodyFontSize = 14;
        public const int ButtonFontSize = 15;
        public const int HeadingFontSize = 15;

        public static readonly Color Button = Color.white;
        public static readonly Color Primary = new Color(0.48f, 0.82f, 1f, 1f);
        public static readonly Color Navigation = new Color(0.86f, 0.93f, 1f, 1f);
        public static readonly Color StatusCard = new Color(0.93f, 0.96f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.78f, 0.74f, 1f);
        public static readonly Color DangerCard = new Color(1f, 0.92f, 0.91f, 1f);
        public static readonly Color Enabled = new Color(0.78f, 1f, 0.82f, 1f);
        public static readonly Color Disabled = new Color(1f, 0.82f, 0.78f, 1f);
    }
}
#endif
