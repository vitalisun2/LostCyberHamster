namespace Vues.GameCore.Quests
{
    /// <summary>Фиксирует предъявленную общую награду и владельца до нажатия Claim.</summary>
    public sealed class DailyCommonRewardSnapshot
    {
        public string ProfileId { get; }
        public long Generation { get; }
        public string SetId { get; }
        public string OriginDate { get; }
        public ResourceType RewardType { get; }
        public int Amount { get; }
        public int RemainingRewards { get; }

        public DailyCommonRewardSnapshot(string profileId, long generation, string setId, string originDate,
            ResourceType rewardType, int amount, int remainingRewards)
        {
            ProfileId = profileId;
            Generation = generation;
            SetId = setId;
            OriginDate = originDate;
            RewardType = rewardType;
            Amount = amount;
            RemainingRewards = remainingRewards;
        }
    }
}
