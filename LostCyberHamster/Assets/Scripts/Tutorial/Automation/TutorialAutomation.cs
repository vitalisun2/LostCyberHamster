namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Даёт runtime и editor automation доступ к tutorial-настройкам без прямого PlayerPrefs.
    /// </summary>
    public static class TutorialAutomation
    {
        public const string AutoPlayKey = TutorialStorage.AutoPlayKey;
        public const string StopAfterStepKey = TutorialStorage.StopAfterStepKey;

        public static bool ShouldAutoPlay()
        {
            return TutorialStorage.IsAutoPlayEnabled();
        }

        public static void SetAutoPlay(bool enabled)
        {
            TutorialStorage.SetAutoPlay(enabled);
        }

        public static void ClearAutoPlay()
        {
            TutorialStorage.ClearAutoPlay();
        }

        public static bool TryGetStopAfterStep(out int step)
        {
            return TutorialStorage.TryGetStopAfterStep(out step);
        }

        public static void SetStopAfterStep(int step)
        {
            TutorialStorage.SetStopAfterStep(step);
        }

        public static void ClearStopAfterStep()
        {
            TutorialStorage.ClearStopAfterStep();
        }

        public static void Clear()
        {
            ClearAutoPlay();
            TutorialStorage.ClearStopAfterStep();
        }
    }
}
