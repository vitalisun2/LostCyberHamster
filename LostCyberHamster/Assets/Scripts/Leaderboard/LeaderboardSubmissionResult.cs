namespace GameManagement.Leaderboard
{
    /// <summary>
    /// Содержит владельца и лучший одиночный забег недели после проверки Unity Leaderboards.
    /// </summary>
    public sealed class LeaderboardSubmissionResult
    {
        public LeaderboardSubmissionResult(
            string playerId,
            int previousWeeklyBestRunScore,
            int weeklyBestRunScore,
            bool isNewRecord)
        {
            PlayerId = playerId;
            PreviousWeeklyBestRunScore = previousWeeklyBestRunScore;
            WeeklyBestRunScore = weeklyBestRunScore;
            IsNewRecord = isNewRecord;
        }

        public string PlayerId { get; }

        public int PreviousWeeklyBestRunScore { get; }

        public int WeeklyBestRunScore { get; }

        public bool IsNewRecord { get; }
    }
}
