namespace GameManagement.Leaderboard
{
    /// <summary>Неизменяемый снимок известного best, закреплённый за контекстом одной попытки.</summary>
    public sealed class WeeklyRecordBaseline
    {
        public bool HadEntry { get; }
        public int Score { get; }
        public string FetchedAtUtc { get; }

        public WeeklyRecordBaseline(WeeklyPersonalBest best)
        {
            HadEntry = best.HadEntry;
            Score = best.Score;
            FetchedAtUtc = best.FetchedAtUtc;
        }
    }
}
