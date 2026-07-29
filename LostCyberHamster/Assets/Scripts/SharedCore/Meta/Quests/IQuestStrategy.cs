namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Рассчитывает прогресс квеста по подходящему событию.
    /// </summary>
    public interface IQuestStrategy
    {
        /// <summary>
        /// Тип квеста, который обрабатывает стратегия.
        /// </summary>
        QuestType Type { get; }

        /// <summary>
        /// Возвращает прогресс от события или ноль, если событие не подходит.
        /// </summary>
        int CalculateProgress(
            QuestDefinition definition,
            QuestEvent questEvent);
    }
}
