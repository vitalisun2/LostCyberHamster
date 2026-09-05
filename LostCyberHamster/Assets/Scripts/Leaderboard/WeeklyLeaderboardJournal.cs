using System;
using System.Collections.Generic;

namespace GameManagement.Leaderboard
{
    /// <summary>Техническая секция общего save envelope: FIFO, подтверждения и кеш рейтингов.</summary>
    [Serializable]
    public sealed class WeeklyLeaderboardJournal
    {
        public List<WeeklyLeaderboardRun> Runs = new();
        public List<LeaderboardSeasonContext> Seasons = new();
        public List<LeaderboardResultsSnapshot> CachedResults = new();
    }
}
