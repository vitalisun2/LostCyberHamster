using System;

namespace GameManagement.Leaderboard
{
    /// <summary>Связывает серверный score с конкретной попыткой и её исходным рекордом.</summary>
    [Serializable]
    public sealed class WeeklyScoreMetadata
    {
        public int schema = 1;
        public string runId;
        public int previousBest;
        public bool hadPreviousEntry;
    }
}
