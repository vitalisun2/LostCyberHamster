using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Хранит контентную награду за завершение всего Daily-набора.
    /// </summary>
    [Serializable]
    public sealed class DailyCommonRewardDefinition
    {
        /// <summary>
        /// Тип общей награды.
        /// </summary>
        public ResourceType RewardType;

        /// <summary>
        /// Размер общей награды.
        /// </summary>
        public int RewardAmount;
    }
}
