using System;

namespace Vues.GameCore.Quests
{
    /// <summary>Разрешает XP конкретного квеста для Claim и всех представлений награды.</summary>
    public static class QuestExperienceRewardPolicy
    {
        public const string FirstMorningQuestId = "story-primary-01_New_York-Morning";
        public const int FirstMorningReward = 60;
        public const int StoryReward = 20;
        public const int DailyReward = 5;

        public static int GetReward(Quest quest) => GetReward(quest?.Definition);

        /// <summary>Цели уровня игрока сохраняют валютную награду и дают ноль XP.</summary>
        public static int GetReward(QuestDefinition definition)
        {
            if (definition == null) return 0;
            if (definition.Category == QuestCategory.Daily) return DailyReward;
            if (definition.Category != QuestCategory.Story) return 0;
            if (string.Equals(definition.StateId, PlayerStateIds.PlayerLevel, StringComparison.Ordinal)) return 0;
            return string.Equals(definition.Id, FirstMorningQuestId, StringComparison.Ordinal)
                ? FirstMorningReward : StoryReward;
        }
    }
}
