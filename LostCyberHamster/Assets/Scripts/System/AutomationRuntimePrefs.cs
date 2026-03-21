using UnityEngine;

namespace Assets.Scripts.System
{
    public static class AutomationRuntimePrefs
    {
        public const string SkipIntroKey = "TestLevel_SkipIntro";

        public static bool ShouldSkipIntro()
        {
            return PlayerPrefs.GetInt(SkipIntroKey, 0) == 1;
        }
    }
}