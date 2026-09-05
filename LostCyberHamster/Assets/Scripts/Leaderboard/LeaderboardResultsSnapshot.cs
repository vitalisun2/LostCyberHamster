using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Leaderboards.Models;

namespace GameManagement.Leaderboard
{
    /// <summary>Хранит рейтинг с владельцем, серверной неделей и временем загрузки на устройстве.</summary>
    [Serializable]
    public sealed class LeaderboardResultsSnapshot
    {
        public string OwnerPlayerId;
        public string Environment;
        public string LeaderboardId;
        public string VersionId;
        public string FetchedAtUtc;
        public List<LeaderboardCachedEntry> Entries = new();
        public LeaderboardCachedEntry Player;
        [NonSerialized] public bool IsPreviousWeek;

        public IReadOnlyList<LeaderboardEntry> Top => Entries.Select(entry => entry.ToEntry()).ToArray();
        public LeaderboardEntry CurrentPlayer => Player?.ToEntry();
    }
}
