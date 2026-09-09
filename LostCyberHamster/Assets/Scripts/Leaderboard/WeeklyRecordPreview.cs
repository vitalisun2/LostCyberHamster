namespace GameManagement.Leaderboard
{
    /// <summary>Сравнение текущей попытки с известной базой; отправку и награду не подтверждает.</summary>
    public sealed class WeeklyRecordPreview
    {
        public WeeklyRunContext Context { get; }
        public int Score { get; }
        public bool IsLocalOnly => string.IsNullOrWhiteSpace(Context.OwnerPlayerId) ||
                                   string.IsNullOrWhiteSpace(Context.VersionId);

        public WeeklyRecordPreview(WeeklyRunContext context, int score)
        {
            Context = context;
            Score = score;
        }
    }
}
