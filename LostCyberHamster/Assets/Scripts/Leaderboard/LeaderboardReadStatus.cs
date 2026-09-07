namespace GameManagement.Leaderboard
{
    /// <summary>Состояние чтения общей таблицы, независимое от отправки прогресса.</summary>
    public enum LeaderboardReadStatus { Connecting, Ready, Offline, Unavailable, AuthenticationRequired, BoardUnavailable, ProfileRecoveryUnavailable }
}
