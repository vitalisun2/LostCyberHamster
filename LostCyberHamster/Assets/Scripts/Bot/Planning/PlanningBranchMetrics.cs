using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит агрегированные метрики одной planning-ветки.
    /// </summary>
    public sealed class PlanningBranchMetrics
    {
        public static PlanningBranchMetrics Empty { get; } = new PlanningBranchMetrics(0, 0, null, 0);

        /// <summary>
        /// Создает набор метрик для ветки планирования.
        /// </summary>
        public PlanningBranchMetrics(int totalEnergyCost, int tapCount, float? firstTriggerX, int actionCount)
        {
            TotalEnergyCost = totalEnergyCost;
            TapCount = tapCount;
            FirstTriggerX = firstTriggerX;
            ActionCount = actionCount;
        }

        public int TotalEnergyCost { get; }
        public int TapCount { get; }
        public float? FirstTriggerX { get; }
        public int ActionCount { get; }

        /// <summary>
        /// Возвращает новые метрики после добавления одного действия.
        /// </summary>
        public PlanningBranchMetrics Append(PlannedAction action)
        {
            return new PlanningBranchMetrics(
                TotalEnergyCost + action.EnergyCost,
                TapCount + (action.Kind == BotActionKind.Tap ? 1 : 0),
                FirstTriggerX ?? action.TriggerX,
                ActionCount + 1);
        }

        public bool IsCheaperOrEquivalentTo(PlanningBranchMetrics other)
        {
            if (other == null)
                return true;

            if (TotalEnergyCost != other.TotalEnergyCost)
                return TotalEnergyCost < other.TotalEnergyCost;

            if (TapCount != other.TapCount)
                return TapCount < other.TapCount;

            return ActionCount <= other.ActionCount;
        }
    }
}
