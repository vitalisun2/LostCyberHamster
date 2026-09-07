using System;
using System.Collections.Generic;
using Unity.Services.Leaderboards.Models;

namespace GameManagement.Leaderboard
{
    /// <summary>Передаёт экрану данные и независимые состояния таблицы, игрока и участия.</summary>
    public sealed class LeaderboardViewSnapshot
    {
        public string LeaderboardId;
        public bool HasTable;
        public bool IsRefreshing;
        public bool IsStale;
        public int ContentVersion;
        public string VersionId;
        public string FetchedAtUtc;
        public string NextResetUtc;
        public IReadOnlyList<LeaderboardEntry> Top = Array.Empty<LeaderboardEntry>();
        public LeaderboardEntry CurrentPlayer;
        public LeaderboardReadStatus Status;
        public LeaderboardPersonalStatus PersonalStatus;
        public LeaderboardParticipationStatus ParticipationStatus;
        public WeeklyLeaderboardRun LatestRun;
    }
}
