namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Проверяет результат выбранного уровня.
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

            return levelResult.LevelId == definition.RequiredLevelId &&
                   levelResult.Stars >= definition.RequiredStars
                ? 1
                : 0;
        }
    }
}
