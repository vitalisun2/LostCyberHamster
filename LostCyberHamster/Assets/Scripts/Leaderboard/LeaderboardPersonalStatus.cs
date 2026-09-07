namespace GameManagement.Leaderboard
{
    /// <summary>Отличает отсутствие личной записи от недоступности её чтения.</summary>
    public enum LeaderboardPersonalStatus { Ready, NoEntry, Unavailable, ProfileRequired }
}
