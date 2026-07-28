using GameManagement.Progress;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Описывает результат завершённого забега и состояние его публикации.
    /// </summary>
    public sealed class RunResultData
    {
        public RunResultData(
            LevelProgressKey levelKey,
            int runScore,
            int weeklyBestRunScore,
            bool isNewRecord,
            bool isLastLevelOfPart,
            RunResultSubmissionState submissionState)
        {
            LevelKey = levelKey;
            RunScore = runScore;
            WeeklyBestRunScore = weeklyBestRunScore;
            IsNewRecord = isNewRecord;
            IsLastLevelOfPart = isLastLevelOfPart;
            SubmissionState = submissionState;
        }

        public LevelProgressKey LevelKey { get; }

        public int RunScore { get; }

        public int WeeklyBestRunScore { get; }

        public bool IsNewRecord { get; }

        public bool IsLastLevelOfPart { get; }

        public RunResultSubmissionState SubmissionState { get; }
    }
}
