using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит агрегированные метрики одной planning-ветки.
    /// </summary>
    public sealed class PlanningBranchMetrics
    {
        public static PlanningBranchMetrics Empty { get; } = new PlanningBranchMetrics(0, null, 0);

        /// <summary>
        /// Создает набор метрик для ветки планирования.
        /// </summary>
        public PlanningBranchMetrics(int totalEnergyCost, float? firstTriggerX, int actionCount)
        {
            TotalEnergyCost = totalEnergyCost;
            FirstTriggerX = firstTriggerX;
            ActionCount = actionCount;
        }

        public int TotalEnergyCost { get; }
        public float? FirstTriggerX { get; }
        public int ActionCount { get; }

        /// <summary>
        /// Возвращает новые метрики после добавления одного действия.
        /// </summary>
        public PlanningBranchMetrics Append(PlannedAction action)
        {
            return new PlanningBranchMetrics(
                TotalEnergyCost + action.EnergyCost,
                FirstTriggerX ?? action.TriggerX,
                ActionCount + 1);
        }
    }
}
