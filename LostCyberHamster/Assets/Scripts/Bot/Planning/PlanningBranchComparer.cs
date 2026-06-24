using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Сравнивает planning-ветки единым порядком для evaluator и pruning.
    /// </summary>
    internal static class PlanningBranchComparer
    {
        /// <summary>
        /// Сравнивает две planning-ветки по pairwise-горизонту; отрицательное значение означает, что left лучше right.
        /// </summary>
        public static int Compare(PlanningBranch left, PlanningBranch right)
        {
            float pairwiseHorizonProjectionWorldShift = GetPairwiseHorizon(left, right);
            return CompareCore(left, right, pairwiseHorizonProjectionWorldShift);
        }

        /// <summary>
        /// Сравнивает две planning-ветки в общем горизонте текущего набора candidates.
        /// </summary>
        public static int CompareAtCommonHorizon(
            PlanningBranch left,
            PlanningBranch right,
            float commonHorizonProjectionWorldShift)
        {
            return CompareCore(left, right, commonHorizonProjectionWorldShift);
        }

        /// <summary>
        /// Возвращает true, если left не хуже right по pairwise planning-правилам.
        /// </summary>
        public static bool IsBetterOrEqual(PlanningBranch left, PlanningBranch right)
        {
            return Compare(left, right) <= 0;
        }

        private static int CompareCore(
            PlanningBranch left,
            PlanningBranch right,
            float commonHorizonProjectionWorldShift)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            int compare = right.Metrics.LifeCollectibleValue.CompareTo(left.Metrics.LifeCollectibleValue);
            if (compare != 0)
                return compare;

            compare = CompareLifeUrgency(left, right);
            if (compare != 0)
                return compare;

            // Сначала сравнивает только часть веток до заданного общего горизонта,
            // чтобы хвост более длинной ветки не искажал первичный priority.
            compare = PlanningBranchMetricsComparer.Compare(
                left.GetMetricsToReach(commonHorizonProjectionWorldShift),
                right.GetMetricsToReach(commonHorizonProjectionWorldShift));
            if (compare != 0)
                return compare;

            return PlanningBranchMetricsComparer.Compare(left.Metrics, right.Metrics);
        }

        private static float GetPairwiseHorizon(PlanningBranch left, PlanningBranch right)
        {
            if (left == null || right == null)
                return 0f;

            return left.FinalProjectionWorldShift < right.FinalProjectionWorldShift
                ? left.FinalProjectionWorldShift
                : right.FinalProjectionWorldShift;
        }

        private static int CompareLifeUrgency(PlanningBranch left, PlanningBranch right)
        {
            if (left.Metrics.LifeCollectibleValue <= 0 || right.Metrics.LifeCollectibleValue <= 0)
                return 0;

            int compare = GetActionCountToFirstLife(left).CompareTo(GetActionCountToFirstLife(right));
            if (compare != 0)
                return compare;

            return GetEnergyCostToFirstLife(left).CompareTo(GetEnergyCostToFirstLife(right));
        }

        private static int GetActionCountToFirstLife(PlanningBranch branch)
        {
            if (branch?.Actions == null)
                return int.MaxValue;

            for (int actionIndex = 0; actionIndex < branch.Actions.Count; actionIndex++)
            {
                if (IsLifeCollectibleAction(branch.Actions[actionIndex]))
                    return actionIndex + 1;
            }

            return int.MaxValue;
        }

        private static int GetEnergyCostToFirstLife(PlanningBranch branch)
        {
            if (branch?.Actions == null)
                return int.MaxValue;

            int energyCost = 0;
            for (int actionIndex = 0; actionIndex < branch.Actions.Count; actionIndex++)
            {
                PlannedAction action = branch.Actions[actionIndex];
                if (action != null)
                    energyCost += action.EnergyCost;

                if (IsLifeCollectibleAction(action))
                    return energyCost;
            }

            return int.MaxValue;
        }

        private static bool IsLifeCollectibleAction(PlannedAction action)
        {
            return action != null
                && action.CollectibleObjectiveValue.Kind == CollectibleKind.Life
                && action.CollectibleObjectiveValue.EffectiveGain > 0;
        }
    }
}
