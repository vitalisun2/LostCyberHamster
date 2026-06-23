namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Сравнивает planning-метрики единым порядком для финального выбора ветки и pruning.
    /// Инвариант порядка: полезная цель должна покрывать стоимость собственного действия,
    /// но не должна оправдывать лишнюю энергию, потраченную до этой цели.
    /// </summary>
    internal static class PlanningBranchMetricsComparer
    {
        /// <summary>
        /// Сравнивает две метрики: отрицательное значение означает, что left лучше right.
        /// Порядок приоритетов:
        /// 1. Life: безопасно подобранная жизнь важнее остальных выгод.
        /// 2. EnergyBeforeFirstMajor: не тратим энергию до первой полезной цели.
        /// 3. MajorObjectiveCount: при равной цене входа берем больше jump-on/crystal целей.
        /// 4. EnergyCost: после смысла ветки минимизируем общий расход, включая цену самого JumpOn.
        /// 5. EnergyCollectibleValue: энергия полезна, но не оправдывает более дорогой экшен к той же major-цели.
        /// 6. CoinCollectibleValue: монетки улучшают только равные по важным критериям ветки.
        /// 7. ActionCount: финальный tie-breaker, чтобы не выбирать лишние действия при полном равенстве.
        /// </summary>
        public static int Compare(PlanningBranchMetrics left, PlanningBranchMetrics right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            // Жизнь не смешивается с обычной энергоэффективностью: если ветка безопасна,
            // life collectible остается самой ценной целью.
            int compare = right.LifeCollectibleValue.CompareTo(left.LifeCollectibleValue);
            if (compare != 0)
                return compare;

            // Ключевой anti-regression критерий: обычный JumpOver/SuperJumpOver до первой
            // полезной цели считается лишним входным расходом, а сам JumpOn-таргет - нет.
            compare = left.EnergyBeforeFirstMajor.CompareTo(right.EnergyBeforeFirstMajor);
            if (compare != 0)
                return compare;

            // Если ветки дошли до полезных целей с одинаковым входным расходом,
            // выигрывает ветка, которая реально обработала больше major objectives.
            compare = right.MajorObjectiveCount.CompareTo(left.MajorObjectiveCount);
            if (compare != 0)
                return compare;

            // Полный расход остается важным: обычный JumpOn за 10 должен выигрывать
            // у SuperJumpOn за 20, если они дают одинаковую полезную цель.
            compare = left.EnergyCost.CompareTo(right.EnergyCost);
            if (compare != 0)
                return compare;

            // Энергия остается полезной secondary objective, но ее нельзя ставить выше
            // цены action: иначе super-вариант выигрывает у обычного только потому,
            // что немного раньше цепляет энергетик после уже достигнутой major-цели.
            compare = right.EnergyCollectibleValue.CompareTo(left.EnergyCollectibleValue);
            if (compare != 0)
                return compare;

            // Coin - низкий приоритет: берем монетки только когда они не ухудшают
            // жизнь, путь к major objective, число major objectives, общий расход и энергию.
            compare = right.CoinCollectibleValue.CompareTo(left.CoinCollectibleValue);
            if (compare != 0)
                return compare;

            // Последний стабильный tie-breaker: при полном равенстве смысловых метрик
            // выбираем более короткую ветку, чтобы не провоцировать лишние switch/action.
            return left.ActionCount.CompareTo(right.ActionCount);
        }

        /// <summary>
        /// Возвращает true, если left не хуже right по planning-приоритетам.
        /// </summary>
        public static bool IsBetterOrEqual(PlanningBranchMetrics left, PlanningBranchMetrics right)
        {
            return Compare(left, right) <= 0;
        }

    }
}
