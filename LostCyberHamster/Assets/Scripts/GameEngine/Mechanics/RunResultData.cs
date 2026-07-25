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
            int levelBestScore,
            int partOfDayTotalScore,
            bool isNewRecord,
            bool isLastLevelOfPart,
            RunResultSubmissionState submissionState)
        {
            LevelKey = levelKey;
            RunScore = runScore;
            LevelBestScore = levelBestScore;
            PartOfDayTotalScore = partOfDayTotalScore;
            IsNewRecord = isNewRecord;
            IsLastLevelOfPart = isLastLevelOfPart;
            SubmissionState = submissionState;
        }

        public LevelProgressKey LevelKey { get; }

        public int RunScore { get; }

        public int LevelBestScore { get; }

        public int PartOfDayTotalScore { get; }

        public bool IsNewRecord { get; }

        public bool IsLastLevelOfPart { get; }

        public RunResultSubmissionState SubmissionState { get; }
    }
}
