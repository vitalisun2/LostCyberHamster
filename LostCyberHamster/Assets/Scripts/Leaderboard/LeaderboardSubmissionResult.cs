namespace GameManagement.Leaderboard
{
    /// <summary>
    /// Содержит авторитетный недельный результат после проверки Unity Leaderboards.
    /// </summary>
    public sealed class LeaderboardSubmissionResult
    {
        public LeaderboardSubmissionResult(
            int levelBestScore,
            int partOfDayTotalScore,
            bool isNewRecord,
            bool wasSubmitted)
        {
            LevelBestScore = levelBestScore;
            PartOfDayTotalScore = partOfDayTotalScore;
            IsNewRecord = isNewRecord;
            WasSubmitted = wasSubmitted;
        }

        public int LevelBestScore { get; }

        public int PartOfDayTotalScore { get; }

        public bool IsNewRecord { get; }

        public bool WasSubmitted { get; }
    }
}
