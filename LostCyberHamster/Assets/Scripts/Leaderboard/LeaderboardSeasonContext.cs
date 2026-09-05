using System;

namespace GameManagement.Leaderboard
{
    /// <summary>Сохраняет последний полученный от сервера идентификатор недели.</summary>
    [Serializable]
    public sealed class LeaderboardSeasonContext
    {
        public string Environment;
        public string LeaderboardId;
        public string VersionId;
        public string NextResetUtc;
    }
}
