namespace GameManagement.Leaderboard
{
    /// <summary>
    /// Содержит лучший одиночный забег недели после проверки Unity Leaderboards.
    /// </summary>
    public sealed class LeaderboardSubmissionResult
    {
        public LeaderboardSubmissionResult(
            int previousWeeklyBestRunScore,
            int weeklyBestRunScore,
            bool isNewRecord)
        {
            PreviousWeeklyBestRunScore = previousWeeklyBestRunScore;
            WeeklyBestRunScore = weeklyBestRunScore;
            IsNewRecord = isNewRecord;
        }

        public int PreviousWeeklyBestRunScore { get; }

        public int WeeklyBestRunScore { get; }

        public bool IsNewRecord { get; }
    }
}
