using System;

namespace GameManagement.Leaderboard
{
    /// <summary>Сохраняет выбранную дневную выплату независимо от применения XP и просмотра рекорда.</summary>
    [Serializable]
    public sealed class WeeklyDailyRewardDecision
    {
        public string RewardId;
        public string FirstConfirmedAtUtc;
        public string UtcDate;
        public int AwardedExperience;
        public bool IsLegacyReward;
    }
}
