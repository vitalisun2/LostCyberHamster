using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Проверяет успешный результат выбранного или любого уровня.
    /// </summary>
    public sealed class LevelResultQuestStrategy : IQuestStrategy
    {
        /// <inheritdoc />
        public QuestType Type => QuestType.LevelResult;

        /// <inheritdoc />
        public int CalculateProgress(
            QuestDefinition definition,
            QuestEvent questEvent)
        {
            if (questEvent is not LevelResultQuestEvent levelResult)
            {
                return 0;
            }

            bool matchesLevel = definition.RequiredLevelId == 0 ||
                                levelResult.LevelId == definition.RequiredLevelId;
            bool matchesLocation =
                string.IsNullOrWhiteSpace(definition.RequiredLocationId) ||
                string.Equals(
                    levelResult.LocationId,
                    definition.RequiredLocationId,
                    StringComparison.OrdinalIgnoreCase);
            bool matchesPartOfDay =
                string.IsNullOrWhiteSpace(definition.RequiredPartOfDayId) ||
                string.Equals(
                    levelResult.PartOfDayId,
                    definition.RequiredPartOfDayId,
                    StringComparison.OrdinalIgnoreCase);
            return matchesLevel &&
                   matchesLocation &&
                   matchesPartOfDay &&
                   levelResult.Stars >= definition.RequiredStars
                ? 1
                : 0;
        }
    }
}
