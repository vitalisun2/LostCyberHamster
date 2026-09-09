using System;

namespace Vues.GameCore.Quests
{
    /// <summary>Сохраняет доступную общую награду после смены дневного набора.</summary>
    [Serializable]
    public sealed class PendingDailyCommonReward
    {
        public string SetId;
        public string OriginDate;
        public ResourceType RewardType;
        public int RewardAmount;
    }
}
