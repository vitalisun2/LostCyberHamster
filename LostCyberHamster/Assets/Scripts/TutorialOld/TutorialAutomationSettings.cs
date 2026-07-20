using UnityEngine;

namespace Assets.Scripts.TutorialOld
{
    public static class TutorialAutomationSettings
    {
        public const string AutoPlayKey = "Tutorial_AutoPlay";
        public const string StopAfterStepKey = "Tutorial_StopAfterStep";

        public static bool ShouldAutoPlay()
        {
            return PlayerPrefs.GetInt(AutoPlayKey, 0) == 1;
        }

        public static bool TryGetStopAfterStep(out int step)
        {
            step = PlayerPrefs.GetInt(StopAfterStepKey, 0);
            return step > 0;
        }
    }
}
