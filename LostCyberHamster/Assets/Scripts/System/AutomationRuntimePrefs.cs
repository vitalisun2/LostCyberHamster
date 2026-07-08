using UnityEngine;

namespace Assets.Scripts.System
{
    public static class AutomationRuntimePrefs
    {
        public const string TestLevelAddressOverrideKey = "TestLevel_Address";
        public const string SkipIntroKey = "TestLevel_SkipIntro";
        public const string TimeScaleOverrideKey = "TestLevel_TimeScale";

        public static bool ShouldSkipIntro()
        {
            return PlayerPrefs.GetInt(SkipIntroKey, 0) == 1;
        }

        /// <summary>
        /// Возвращает true, если текущий Play Mode запущен automation-мостом для test-level validation.
        /// </summary>
        public static bool IsTestLevelAutomationRun()
        {
            return PlayerPrefs.HasKey(TestLevelAddressOverrideKey) || ShouldSkipIntro();
        }

        /// <summary>
        /// Returns true and the override timescale if one was explicitly set by TestLevelLauncher.
        /// When not set, the caller should apply its own default logic.
        /// </summary>
        public static bool TryGetTimeScaleOverride(out float timeScale)
        {
            if (PlayerPrefs.HasKey(TimeScaleOverrideKey))
            {
                timeScale = PlayerPrefs.GetFloat(TimeScaleOverrideKey, 1.0f);
                return true;
            }

            timeScale = 0f;
            return false;
        }
    }
}
