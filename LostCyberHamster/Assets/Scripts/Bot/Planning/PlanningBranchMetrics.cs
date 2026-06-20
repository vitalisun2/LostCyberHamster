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
        public static PlanningBranchMetrics Empty { get; } = new PlanningBranchMetrics(0, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Создает набор метрик для ветки планирования.
        /// </summary>
        public PlanningBranchMetrics(
            int totalEnergyCost,
            int tapCount,
            int actionCount,
            int majorObjectiveCount,
            int lifeCollectibleValue,
            int energyCollectibleValue,
            int crystalCollectibleValue,
            int coinCollectibleValue)
        {
            TotalEnergyCost = totalEnergyCost;
            TapCount = tapCount;
            ActionCount = actionCount;
            MajorObjectiveCount = majorObjectiveCount;
            LifeCollectibleValue = lifeCollectibleValue;
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
        /// Суммарное число основных целей: jump-on target, полезная энергия, crystal.
        /// </summary>
        public int MajorObjectiveCount { get; }

        /// <summary>
        /// Суммарная ценность подобранных life collectables.
        /// </summary>
        public int LifeCollectibleValue { get; }

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
                MajorObjectiveCount + GetMajorObjectiveCount(action),
                LifeCollectibleValue + GetCollectibleValue(action, CollectibleKind.Life),
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

            if (CoinCollectibleValue != other.CoinCollectibleValue)
                return CoinCollectibleValue > other.CoinCollectibleValue;

            if (TapCount != other.TapCount)
                return TapCount < other.TapCount;

            return ActionCount <= other.ActionCount;
        }

        private static int GetMajorObjectiveCount(PlannedAction action)
        {
            if (action == null)
                return 0;

            int count = action.FulfillsJumpOnObjective ? 1 : 0;
            CollectibleKind collectibleKind = action.CollectibleObjectiveValue.Kind;
            if (collectibleKind == CollectibleKind.Energy
                || collectibleKind == CollectibleKind.Crystal)
            {
                count++;
            }

            return count;
        }

        private static int GetCollectibleValue(PlannedAction action, CollectibleKind collectibleKind)
        {
            if (action == null || action.CollectibleObjectiveValue.Kind != collectibleKind)
                return 0;

            return action.CollectibleObjectiveValue.EffectiveGain;
        }

        internal int CompareObjectivePriority(PlanningBranchMetrics other)
        {
            int compare = CompareDescending(LifeCollectibleValue, other?.LifeCollectibleValue ?? 0);
            if (compare != 0)
                return compare;

            return CompareDescending(MajorObjectiveCount, other?.MajorObjectiveCount ?? 0);
        }

        private static int CompareDescending(int left, int right)
        {
            return right.CompareTo(left);
        }
    }
}
