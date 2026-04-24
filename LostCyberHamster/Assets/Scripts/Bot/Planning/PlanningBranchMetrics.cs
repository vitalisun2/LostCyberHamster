using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит агрегированные метрики одной planning-ветки.
    /// </summary>
    public sealed class PlanningBranchMetrics
    {
        /// <summary>
        /// Пустые метрики ветки.
        /// </summary>
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

        /// <summary>
        /// Суммарная стоимость энергии.
        /// </summary>
        public int TotalEnergyCost { get; }

        /// <summary>
        /// Число смен линий.
        /// </summary>
        public int TapCount { get; }

        /// <summary>
        /// Точка запуска первого действия.
        /// </summary>
        public float? FirstTriggerX { get; }

        /// <summary>
        /// Число действий в ветке.
        /// </summary>
        public int ActionCount { get; }

        /// <summary>
        /// Возвращает новые метрики после добавления одного действия.
        /// </summary>
        public PlanningBranchMetrics Append(PlannedAction action)
        {
            return new PlanningBranchMetrics(
                TotalEnergyCost + action.EnergyCost,
                TapCount + (action.Kind == BotActionKind.SwitchLane ? 1 : 0),
                FirstTriggerX ?? action.TriggerX,
                ActionCount + 1);
        }

            /// <summary>
            /// Сравнивает стоимость двух веток.
            /// </summary>
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
