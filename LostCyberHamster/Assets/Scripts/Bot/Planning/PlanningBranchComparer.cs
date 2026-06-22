namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Сравнивает planning-ветки единым порядком для evaluator и pruning.
    /// </summary>
    internal static class PlanningBranchComparer
    {
        /// <summary>
        /// Сравнивает две planning-ветки; отрицательное значение означает, что left лучше right.
        /// </summary>
        public static int Compare(PlanningBranch left, PlanningBranch right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            float commonHorizonProjectionWorldShift = left.FinalProjectionWorldShift < right.FinalProjectionWorldShift
                ? left.FinalProjectionWorldShift
                : right.FinalProjectionWorldShift;

            // Сначала сравнивает только часть веток до общего горизонта,
            // чтобы хвост более длинной ветки не искажал первичный priority.
            int compare = PlanningBranchMetricsComparer.Compare(
                left.GetMetricsToReach(commonHorizonProjectionWorldShift),
                right.GetMetricsToReach(commonHorizonProjectionWorldShift));
            if (compare != 0)
                return compare;

            return PlanningBranchMetricsComparer.Compare(left.Metrics, right.Metrics);
        }

        /// <summary>
        /// Возвращает true, если left не хуже right по тем же правилам, что и финальный evaluator.
        /// </summary>
        public static bool IsBetterOrEqual(PlanningBranch left, PlanningBranch right)
        {
            return Compare(left, right) <= 0;
        }
    }
}
