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
        public static PlanningBranchMetrics Empty { get; } = new PlanningBranchMetrics(0, 0, 0, 0, null, 0, 0, 0, 0, 0);

        /// <summary>
        /// Создает набор метрик для ветки планирования.
        /// </summary>
        public PlanningBranchMetrics(
            int totalEnergyCost,
            int tapCount,
            int actionCount,
            int jumpOnObjectiveCount,
            int? firstJumpOnObjectiveTargetIndex,
            int lifeCollectibleValue,
            int criticalEnergyCollectibleValue,
            int energyCollectibleValue,
            int crystalCollectibleValue,
            int coinCollectibleValue)
        {
            TotalEnergyCost = totalEnergyCost;
            TapCount = tapCount;
            ActionCount = actionCount;
            JumpOnObjectiveCount = jumpOnObjectiveCount;
            FirstJumpOnObjectiveTargetIndex = firstJumpOnObjectiveTargetIndex;
            LifeCollectibleValue = lifeCollectibleValue;
            CriticalEnergyCollectibleValue = criticalEnergyCollectibleValue;
            EnergyCollectibleValue = energyCollectibleValue;
            CrystalCollectibleValue = crystalCollectibleValue;
            CoinCollectibleValue = coinCollectibleValue;
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
        /// Суммарная ценность подобранных life collectables.
        /// </summary>
        public int LifeCollectibleValue { get; }

        /// <summary>
        /// Суммарная ценность energy collectables при энергии не выше порога охоты за target.
        /// </summary>
        public int CriticalEnergyCollectibleValue { get; }

        /// <summary>
        /// Суммарная ценность подобранных energy collectables.
        /// </summary>
        public int EnergyCollectibleValue { get; }

        /// <summary>
        /// Суммарная ценность подобранных crystal collectables.
        /// </summary>
        public int CrystalCollectibleValue { get; }

        /// <summary>
        /// Суммарная ценность подобранных coin collectables.
        /// </summary>
        public int CoinCollectibleValue { get; }

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
                FirstJumpOnObjectiveTargetIndex ?? GetJumpOnObjectiveTargetIndex(action),
                LifeCollectibleValue + GetCollectibleValue(action, CollectibleKind.Life),
                CriticalEnergyCollectibleValue + GetCriticalEnergyCollectibleValue(action),
                EnergyCollectibleValue + GetCollectibleValue(action, CollectibleKind.Energy),
                CrystalCollectibleValue + GetCollectibleValue(action, CollectibleKind.Crystal),
                CoinCollectibleValue + GetCollectibleValue(action, CollectibleKind.Coin));
        }

        /// <summary>
        /// Сравнивает стоимость двух веток.
        /// </summary>
        public bool IsCheaperOrEquivalentTo(PlanningBranchMetrics other)
        {
            if (other == null)
                return true;

            int objectivePriority = CompareObjectivePriority(other);
            if (objectivePriority != 0)
                return objectivePriority < 0;

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

        private static int GetCollectibleValue(PlannedAction action, CollectibleKind collectibleKind)
        {
            if (action == null || action.CollectibleObjectiveValue.Kind != collectibleKind)
                return 0;

            return action.CollectibleObjectiveValue.EffectiveGain;
        }

        private static int GetCriticalEnergyCollectibleValue(PlannedAction action)
        {
            if (action == null
                || action.CollectibleObjectiveValue.Kind != CollectibleKind.Energy
                || !action.CollectibleObjectiveValue.IsCriticalEnergy)
            {
                return 0;
            }

            return action.CollectibleObjectiveValue.EffectiveGain;
        }

        internal int CompareObjectivePriority(PlanningBranchMetrics other)
        {
            int compare = CompareDescending(LifeCollectibleValue, other?.LifeCollectibleValue ?? 0);
            if (compare != 0)
                return compare;

            compare = CompareDescending(CriticalEnergyCollectibleValue, other?.CriticalEnergyCollectibleValue ?? 0);
            if (compare != 0)
                return compare;

            compare = CompareJumpOnObjectivePriority(other);
            if (compare != 0)
                return compare;

            compare = CompareDescending(EnergyCollectibleValue, other?.EnergyCollectibleValue ?? 0);
            if (compare != 0)
                return compare;

            compare = CompareDescending(CrystalCollectibleValue, other?.CrystalCollectibleValue ?? 0);
            if (compare != 0)
                return compare;

            return CompareDescending(CoinCollectibleValue, other?.CoinCollectibleValue ?? 0);
        }

        private int CompareJumpOnObjectivePriority(PlanningBranchMetrics other)
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

        private static int CompareDescending(int left, int right)
        {
            return right.CompareTo(left);
        }
    }
}
