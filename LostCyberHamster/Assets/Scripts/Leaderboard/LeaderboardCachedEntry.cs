using System;
using Unity.Services.Leaderboards.Models;

namespace GameManagement.Leaderboard
{
    /// <summary>Сериализуемая строка последнего загруженного рейтинга.</summary>
    [Serializable]
    public sealed class LeaderboardCachedEntry
    {
        public string PlayerId;
        public string PlayerName;
        public int Rank;
        public double Score;

        public static LeaderboardCachedEntry FromEntry(LeaderboardEntry entry) => entry == null ? null :
            new LeaderboardCachedEntry
            {
                PlayerId = entry.PlayerId, PlayerName = entry.PlayerName,
                Rank = entry.Rank, Score = entry.Score
            };

        public LeaderboardEntry ToEntry() => new(PlayerId, PlayerName, Rank, Score);
    }
}
