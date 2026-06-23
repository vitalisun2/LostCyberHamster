using System.Collections.Generic;
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
        /// Суммарное число основных целей: jump-on target и crystal.
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
        /// Собирает метрики из полной цепочки действий с учетом подготовительных действий составной objective-цепочки.
        /// </summary>
        public static PlanningBranchMetrics FromActions(IReadOnlyList<PlannedAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return Empty;

            return FromActionPrefix(actions, actions.Count);
        }

        /// <summary>
        /// Собирает метрики из начального участка цепочки действий.
        /// </summary>
        internal static PlanningBranchMetrics FromActionPrefix(
            IReadOnlyList<PlannedAction> actions,
            int actionCount)
        {
            if (actions == null || actionCount <= 0)
                return Empty;

            int effectiveActionCount = actionCount < actions.Count ? actionCount : actions.Count;
            if (effectiveActionCount == 0)
                return Empty;

            int firstMajorActionIndex = FindFirstMajorActionIndex(actions, effectiveActionCount);
            int energyCost = 0;
            int energyBeforeFirstMajor = 0;
            int countedActionCount = 0;
            int majorObjectiveCount = 0;
            int lifeCollectibleValue = 0;
            int energyCollectibleValue = 0;
            int crystalCollectibleValue = 0;
            int coinCollectibleValue = 0;

            for (int actionIndex = 0; actionIndex < effectiveActionCount; actionIndex++)
            {
                PlannedAction action = actions[actionIndex];
                int actionEnergyCost = action != null ? action.EnergyCost : 0;

                energyCost += actionEnergyCost;
                countedActionCount++;
                majorObjectiveCount += GetMajorObjectiveCount(action);
                lifeCollectibleValue += GetCollectibleValue(action, CollectibleKind.Life);
                energyCollectibleValue += GetCollectibleValue(action, CollectibleKind.Energy);
                crystalCollectibleValue += GetCollectibleValue(action, CollectibleKind.Crystal);
                coinCollectibleValue += GetCollectibleValue(action, CollectibleKind.Coin);

                if ((firstMajorActionIndex < 0 || actionIndex < firstMajorActionIndex)
                    && !IsSetupForFirstMajor(actions, actionIndex, firstMajorActionIndex))
                {
                    energyBeforeFirstMajor += actionEnergyCost;
                }
            }

            return new PlanningBranchMetrics(
                energyCost,
                energyBeforeFirstMajor,
                countedActionCount,
                majorObjectiveCount,
                lifeCollectibleValue,
                energyCollectibleValue,
                crystalCollectibleValue,
                coinCollectibleValue);
        }

        private static int FindFirstMajorActionIndex(
            IReadOnlyList<PlannedAction> actions,
            int actionCount)
        {
            for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                if (GetMajorObjectiveCount(actions[actionIndex]) > 0)
                    return actionIndex;
            }

            return -1;
        }

        private static bool IsSetupForFirstMajor(
            IReadOnlyList<PlannedAction> actions,
            int actionIndex,
            int firstMajorActionIndex)
        {
            if (firstMajorActionIndex <= 0 || actionIndex != firstMajorActionIndex - 1)
                return false;

            PlannedAction setupAction = actions[actionIndex];
            PlannedAction firstMajorAction = actions[firstMajorActionIndex];
            if (setupAction == null || firstMajorAction == null)
                return false;

            return IsRoofEntryAction(setupAction.Kind)
                && IsFromRoofJumpOnAction(firstMajorAction.Kind);
        }

        private static bool IsRoofEntryAction(BotActionKind actionKind)
        {
            return actionKind == BotActionKind.JumpOnRoof
                || actionKind == BotActionKind.SuperJumpOnRoof;
        }

        private static bool IsFromRoofJumpOnAction(BotActionKind actionKind)
        {
            return actionKind == BotActionKind.JumpOnFromRoof
                || actionKind == BotActionKind.SuperJumpOnFromRoof;
        }

        private static int GetMajorObjectiveCount(PlannedAction action)
        {
            if (action == null)
                return 0;

            int count = action.FulfillsJumpOnObjective ? 1 : 0;
            if (action.CollectibleObjectiveValue.Kind == CollectibleKind.Crystal)
                count++;

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
