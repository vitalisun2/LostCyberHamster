using System;

namespace GameManagement.Leaderboard
{
    /// <summary>Хранит известный личный результат конкретного профиля и серверной недели.</summary>
    [Serializable]
    public sealed class WeeklyPersonalBest
    {
        public string OwnerPlayerId;
        public string ProfileId;
        public string Environment;
        public string LeaderboardId;
        public string VersionId;
        public string FetchedAtUtc;
        public bool HadEntry;
        public int Score;
    }
}
