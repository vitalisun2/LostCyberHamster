namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Сравнивает planning-метрики по единому порядку приоритетов для pruning и финального выбора ветки.
    /// </summary>
    internal static class PlanningBranchMetricsComparer
    {
        /// <summary>
        /// Сравнивает две метрики: отрицательное значение означает, что left лучше right.
        /// </summary>
        public static int Compare(PlanningBranchMetrics left, PlanningBranchMetrics right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            int compare = CompareDescending(left.LifeCollectibleValue, right.LifeCollectibleValue);
            if (compare != 0)
                return compare;

            compare = left.EnergyBeforeFirstMajor.CompareTo(right.EnergyBeforeFirstMajor);
            if (compare != 0)
                return compare;

            compare = CompareDescending(left.MajorObjectiveCount, right.MajorObjectiveCount);
            if (compare != 0)
                return compare;

            compare = left.EnergyCost.CompareTo(right.EnergyCost);
            if (compare != 0)
                return compare;

            compare = CompareDescending(left.CoinCollectibleValue, right.CoinCollectibleValue);
            if (compare != 0)
                return compare;

            return left.ActionCount.CompareTo(right.ActionCount);
        }

        /// <summary>
        /// Возвращает true, если left не хуже right по planning-приоритетам.
        /// </summary>
        public static bool IsBetterOrEqual(PlanningBranchMetrics left, PlanningBranchMetrics right)
        {
            return Compare(left, right) <= 0;
        }

        private static int CompareDescending(int left, int right)
        {
            return right.CompareTo(left);
        }
    }
}
