namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Считает события одного действия.
    /// </summary>
    public sealed class ActionCounterQuestStrategy : IQuestStrategy
    {
        /// <inheritdoc />
        public QuestType Type => QuestType.ActionCounter;

        /// <inheritdoc />
        public int CalculateProgress(
            QuestDefinition definition,
            QuestEvent questEvent)
        {
            if (questEvent is not ActionQuestEvent actionEvent)
            {
                return 0;
            }

            return actionEvent.ActionId == definition.ActionId
                ? actionEvent.Count
                : 0;
        }
    }
}
