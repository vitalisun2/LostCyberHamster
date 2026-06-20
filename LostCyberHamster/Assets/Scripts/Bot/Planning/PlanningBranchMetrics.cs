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
            int energyCost,
            int energyBeforeFirstMajor,
            int actionCount,
            int majorObjectiveCount,
            int lifeCollectibleValue,
            int energyCollectibleValue,
            int crystalCollectibleValue,
            int coinCollectibleValue)
        {
            EnergyCost = energyCost;
            EnergyBeforeFirstMajor = energyBeforeFirstMajor;
            ActionCount = actionCount;
            MajorObjectiveCount = majorObjectiveCount;
            LifeCollectibleValue = lifeCollectibleValue;
            EnergyCollectibleValue = energyCollectibleValue;
            CrystalCollectibleValue = crystalCollectibleValue;
            CoinCollectibleValue = coinCollectibleValue;
        }

        /// <summary>
        /// Суммарная стоимость энергии всех действий ветки.
        /// </summary>
        public int EnergyCost { get; }

        /// <summary>
        /// Стоимость энергии, потраченной до первого major objective в ветке.
        /// </summary>
        public int EnergyBeforeFirstMajor { get; }

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
            int actionEnergyCost = action != null ? action.EnergyCost : 0;
            int actionMajorObjectiveCount = GetMajorObjectiveCount(action);
            int energyBeforeFirstMajor = EnergyBeforeFirstMajor;
            if (MajorObjectiveCount == 0 && actionMajorObjectiveCount == 0)
                energyBeforeFirstMajor += actionEnergyCost;

            return new PlanningBranchMetrics(
                EnergyCost + actionEnergyCost,
                energyBeforeFirstMajor,
                ActionCount + 1,
                MajorObjectiveCount + actionMajorObjectiveCount,
                LifeCollectibleValue + GetCollectibleValue(action, CollectibleKind.Life),
                EnergyCollectibleValue + GetCollectibleValue(action, CollectibleKind.Energy),
                CrystalCollectibleValue + GetCollectibleValue(action, CollectibleKind.Crystal),
                CoinCollectibleValue + GetCollectibleValue(action, CollectibleKind.Coin));
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

    }
}
