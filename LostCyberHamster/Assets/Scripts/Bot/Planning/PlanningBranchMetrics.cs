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
        public static PlanningBranchMetrics Empty { get; } = new PlanningBranchMetrics(0, 0, 0, 0, null);

        /// <summary>
        /// Создает набор метрик для ветки планирования.
        /// </summary>
        public PlanningBranchMetrics(
            int totalEnergyCost,
            int tapCount,
            int actionCount,
            int jumpOnObjectiveCount,
            int? firstJumpOnObjectiveTargetIndex)
        {
            TotalEnergyCost = totalEnergyCost;
            TapCount = tapCount;
            ActionCount = actionCount;
            JumpOnObjectiveCount = jumpOnObjectiveCount;
            FirstJumpOnObjectiveTargetIndex = firstJumpOnObjectiveTargetIndex;
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
        /// Число действий в ветке.
        /// </summary>
        public int ActionCount { get; }

        /// <summary>
        /// Число выполненных high-priority jump-on objectives.
        /// </summary>
        public int JumpOnObjectiveCount { get; }

        /// <summary>
        /// World-index первого high-priority jump-on target, выполненного веткой.
        /// </summary>
        public int? FirstJumpOnObjectiveTargetIndex { get; }

        /// <summary>
        /// Возвращает новые метрики после добавления одного действия.
        /// </summary>
        public PlanningBranchMetrics Append(PlannedAction action)
        {
            return new PlanningBranchMetrics(
                TotalEnergyCost + action.EnergyCost,
                TapCount + (BotActionKindRules.ConsumesTap(action.Kind) ? 1 : 0),
                ActionCount + 1,
                JumpOnObjectiveCount + (action.FulfillsJumpOnObjective ? 1 : 0),
                FirstJumpOnObjectiveTargetIndex ?? GetJumpOnObjectiveTargetIndex(action));
        }

        /// <summary>
        /// Сравнивает стоимость двух веток.
        /// </summary>
        public bool IsCheaperOrEquivalentTo(PlanningBranchMetrics other)
        {
            if (other == null)
                return true;

            int jumpOnObjectivePriority = CompareJumpOnObjectivePriority(other);
            if (jumpOnObjectivePriority != 0)
                return jumpOnObjectivePriority < 0;

            if (TotalEnergyCost != other.TotalEnergyCost)
                return TotalEnergyCost < other.TotalEnergyCost;

            if (TapCount != other.TapCount)
                return TapCount < other.TapCount;

            return ActionCount <= other.ActionCount;
        }

        private static int? GetJumpOnObjectiveTargetIndex(PlannedAction action)
        {
            return action != null && action.FulfillsJumpOnObjective
                ? action.TargetObstacleIndex
                : null;
        }

        internal int CompareJumpOnObjectivePriority(PlanningBranchMetrics other)
        {
            if (other == null)
                return -1;

            bool hasObjective = FirstJumpOnObjectiveTargetIndex.HasValue;
            bool otherHasObjective = other.FirstJumpOnObjectiveTargetIndex.HasValue;
            if (hasObjective != otherHasObjective)
                return hasObjective ? -1 : 1;

            if (hasObjective && FirstJumpOnObjectiveTargetIndex.Value != other.FirstJumpOnObjectiveTargetIndex.Value)
                return FirstJumpOnObjectiveTargetIndex.Value.CompareTo(other.FirstJumpOnObjectiveTargetIndex.Value);

            return other.JumpOnObjectiveCount.CompareTo(JumpOnObjectiveCount);
        }
    }
}
