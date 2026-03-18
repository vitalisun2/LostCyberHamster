using System;
using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Оценка и выбор лучшей ветви планировщика.
    /// Лексикографическое сравнение: AllStepsSafe → BestRank → TotalProfit → NetEnergy → TotalCost → step-by-step tiebreak.
    /// </summary>
    public static class BranchEvaluator
    {
        /// <summary>
        /// Сортирует кандидатов от лучшего к худшему.
        /// </summary>
        public static void Sort(List<ChainCandidate> candidates)
        {
            candidates.Sort(Compare);
        }

        /// <summary>
        /// Выбирает лучшего кандидата (первый после сортировки). Null если список пуст.
        /// </summary>
        public static ChainCandidate SelectBest(List<ChainCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            Sort(candidates);
            return candidates[0];
        }

        /// <summary>
        /// Сравнение двух ветвей. Отрицательный результат — a лучше b.
        /// </summary>
        public static int Compare(ChainCandidate a, ChainCandidate b)
        {
            var oa = a.Outcome;
            var ob = b.Outcome;

            if (oa.AllStepsSafe != ob.AllStepsSafe)
                return oa.AllStepsSafe ? -1 : 1;

            if (oa.BestRank != ob.BestRank)
                return oa.BestRank.CompareTo(ob.BestRank);

            if (oa.TotalProfit != ob.TotalProfit)
                return ob.TotalProfit.CompareTo(oa.TotalProfit);

            if (oa.NetEnergyGain != ob.NetEnergyGain)
                return ob.NetEnergyGain.CompareTo(oa.NetEnergyGain);

            if (oa.TotalEnergyCost != ob.TotalEnergyCost)
                return oa.TotalEnergyCost.CompareTo(ob.TotalEnergyCost);

            // Tiebreak: пошаговое сравнение приоритетов шагов
            int minSteps = Math.Min(a.Steps.Count, b.Steps.Count);
            for (int i = 0; i < minSteps; i++)
            {
                int cmp = ChainStep.ComparePriority(a.Steps[i], b.Steps[i]);
                if (cmp != 0)
                    return -cmp;
            }

            // Более длинная ветвь предпочтительнее (больше просчитано)
            return b.Steps.Count.CompareTo(a.Steps.Count);
        }
    }
}
