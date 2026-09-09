namespace GameManagement.Leaderboard
{
    /// <summary>Подтверждённый сервером рекорд с уже сохранённой выплатой и отдельным ID показа.</summary>
    public sealed class WeeklyRecordNotification
    {
        public string NotificationId { get; }
        public string OwnerPlayerId { get; }
        public string ProfileId { get; }
        public string Environment { get; }
        public string LeaderboardId { get; }
        public string VersionId { get; }
        public string RunId { get; }
        public int Score { get; }
        public bool IsFirstEntry { get; }
        public int AwardedExperience { get; }

        public WeeklyRecordNotification(WeeklyLeaderboardRun run)
        {
            NotificationId = GetNotificationId(run);
            OwnerPlayerId = run.OwnerPlayerId;
            ProfileId = run.ProfileId;
            Environment = run.Environment;
            LeaderboardId = run.LeaderboardId;
            VersionId = run.VersionId;
            RunId = run.RunId;
            Score = run.Score;
            IsFirstEntry = !run.HadPreviousEntry;
            AwardedExperience = run.RewardDecision?.AwardedExperience ?? 0;
        }

        internal static string GetNotificationId(WeeklyLeaderboardRun run) =>
            Part(run.OwnerPlayerId) + Part(run.ProfileId) + Part(run.Environment) +
            Part(run.LeaderboardId) + Part(run.VersionId) + Part(run.RunId);

        private static string Part(string value) => $"{value?.Length ?? 0}:{value}";
    }
}
