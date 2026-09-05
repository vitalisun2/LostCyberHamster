namespace GameManagement.Leaderboard
{
    /// <summary>Фиксирует известную серверную неделю и локальный профиль до начала забега.</summary>
    public sealed class WeeklyRunContext
    {
        public string OwnerPlayerId { get; }
        public string ProfileId { get; }
        public long Generation { get; }
        public string Environment { get; }
        public string LeaderboardId { get; }
        public string VersionId { get; }

        public WeeklyRunContext(string ownerPlayerId, string profileId, long generation,
            string environment, string leaderboardId, string versionId)
        {
            OwnerPlayerId = ownerPlayerId;
            ProfileId = profileId;
            Generation = generation;
            Environment = environment;
            LeaderboardId = leaderboardId;
            VersionId = versionId;
        }
    }
}
