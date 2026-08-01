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
            return matchesLevel &&
                   levelResult.Stars >= definition.RequiredStars
                ? 1
                : 0;
        }
    }
}
