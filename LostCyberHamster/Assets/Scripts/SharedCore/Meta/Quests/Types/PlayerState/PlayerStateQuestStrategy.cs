namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Проверяет постоянное состояние игрока.
    /// </summary>
    public sealed class PlayerStateQuestStrategy : IQuestStrategy
    {
        /// <inheritdoc />
        public QuestType Type => QuestType.PlayerState;

        /// <inheritdoc />
        public int CalculateProgress(
            QuestDefinition definition,
            QuestEvent questEvent)
        {
            if (questEvent is not PlayerStateQuestEvent playerState)
            {
                return 0;
            }

            return playerState.StateId == definition.StateId &&
                   playerState.EntityId == definition.EntityId &&
                   playerState.Value >= definition.RequiredValue
                ? 1
                : 0;
        }
    }
}
