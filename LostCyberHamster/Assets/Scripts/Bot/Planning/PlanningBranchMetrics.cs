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
            int routeEnergyCost,
            int objectiveEnergyCost,
            int actionCount,
            int majorObjectiveCount,
            int lifeCollectibleValue,
            int energyCollectibleValue,
            int crystalCollectibleValue,
            int coinCollectibleValue)
        {
            RouteEnergyCost = routeEnergyCost;
            ObjectiveEnergyCost = objectiveEnergyCost;
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
        public int TotalEnergyCost => RouteEnergyCost + ObjectiveEnergyCost;

        /// <summary>
        /// Энергия, потраченная на прохождение маршрута без получения major objective.
        /// </summary>
        public int RouteEnergyCost { get; }

        /// <summary>
        /// Энергия, потраченная действием, которое получает major objective.
        /// </summary>
        public int ObjectiveEnergyCost { get; }

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
            bool isObjectiveEnergyCost = IsObjectiveEnergyCost(action);
            int routeEnergyCost = isObjectiveEnergyCost ? 0 : action.EnergyCost;
            int objectiveEnergyCost = isObjectiveEnergyCost ? action.EnergyCost : 0;

            return new PlanningBranchMetrics(
                RouteEnergyCost + routeEnergyCost,
                ObjectiveEnergyCost + objectiveEnergyCost,
                ActionCount + 1,
                MajorObjectiveCount + GetMajorObjectiveCount(action),
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

        private static bool IsObjectiveEnergyCost(PlannedAction action)
        {
            if (action == null || action.EnergyCost <= 0)
                return false;

            if (action.FulfillsJumpOnObjective)
                return true;

            CollectibleKind collectibleKind = action.CollectibleObjectiveValue.Kind;
            return collectibleKind == CollectibleKind.Energy
                || collectibleKind == CollectibleKind.Crystal;
        }

        private static int GetCollectibleValue(PlannedAction action, CollectibleKind collectibleKind)
        {
            if (action == null || action.CollectibleObjectiveValue.Kind != collectibleKind)
                return 0;

            return action.CollectibleObjectiveValue.EffectiveGain;
        }

    }
}
