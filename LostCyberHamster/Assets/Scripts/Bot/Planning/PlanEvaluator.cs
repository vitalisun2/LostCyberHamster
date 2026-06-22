using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Выбирает лучшую planning-ветку и считает итоговый score плана.
    /// </summary>
    public sealed class PlanEvaluator
    {
        private const float LifeObjectiveScore = 10000000f;
        private const float MajorObjectiveScore = 100000f;
        private const float EnergyCostPenalty = 100f;

        /// <summary>
        /// Возвращает лучшую ветку из набора рассчитанных кандидатов.
        /// </summary>
        public PlanningBranch SelectBest(IReadOnlyList<PlanningBranch> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            PlanningBranch best = candidates[0];
            for (int candidateIndex = 1; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (CompareBranches(candidates[candidateIndex], best) < 0)
                    best = candidates[candidateIndex];
            }

            return best;
        }

        /// <summary>
        /// Выбирает dead-end ветку по обычным branch-priority правилам.
        /// </summary>
        internal PlanningDeadEndBranch SelectBestDeadEnd(IReadOnlyList<PlanningDeadEndBranch> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            PlanningDeadEndBranch best = candidates[0];
            for (int candidateIndex = 1; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (CompareDeadEndBranches(candidates[candidateIndex], best) < 0)
                    best = candidates[candidateIndex];
            }

            return best;
        }

        /// <summary>
        /// Считает score итогового плана по последовательности действий.
        /// </summary>
        public float Score(IReadOnlyList<PlannedAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return 0f;

            int totalEnergyCost = 0;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                totalEnergyCost += actions[actionIndex].EnergyCost;

            return 1000f
                + GetObjectiveScore(actions)
                - totalEnergyCost * EnergyCostPenalty
                + GetCoinScore(actions);
        }

        private static float GetObjectiveScore(IReadOnlyList<PlannedAction> actions)
        {
            float score = 0f;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                PlannedAction action = actions[actionIndex];
                if (action == null)
                    continue;

                if (action.CollectibleObjectiveValue.Kind == CollectibleKind.Life)
                    score += action.CollectibleObjectiveValue.EffectiveGain * LifeObjectiveScore;

                if (action.FulfillsJumpOnObjective)
                    score += MajorObjectiveScore;

                if (action.CollectibleObjectiveValue.Kind == CollectibleKind.Energy
                    || action.CollectibleObjectiveValue.Kind == CollectibleKind.Crystal)
                {
                    score += MajorObjectiveScore;
                }
            }

            return score;
        }

        private static float GetCoinScore(IReadOnlyList<PlannedAction> actions)
        {
            float score = 0f;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                PlannedAction action = actions[actionIndex];
                if (action?.CollectibleObjectiveValue.Kind != CollectibleKind.Coin)
                    continue;

                score += action.CollectibleObjectiveValue.EffectiveGain;
            }

            return score;
        }

        /// <summary>
        /// Сравнивает две planning-ветки.
        /// </summary>
        private static int CompareBranches(PlanningBranch left, PlanningBranch right)
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
            // Сначала сравниваем только часть веток до общего горизонта,
            // чтобы будущий хвост более длинной ветки не искажал первичное сравнение.
            int compare = PlanningBranchMetricsComparer.Compare(
                left.GetMetricsToReach(commonHorizonProjectionWorldShift),
                right.GetMetricsToReach(commonHorizonProjectionWorldShift));
            if (compare != 0)
                return compare;

            return PlanningBranchMetricsComparer.Compare(left.Metrics, right.Metrics);
        }

        /// <summary>
        /// Сравнивает dead-end ветки по тем же приоритетам, что и обычные ветки.
        /// </summary>
        private static int CompareDeadEndBranches(PlanningDeadEndBranch left, PlanningDeadEndBranch right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left?.Branch == null)
                return 1;

            if (right?.Branch == null)
                return -1;

            PlanningBranch leftBranch = left.Branch;
            PlanningBranch rightBranch = right.Branch;

            return CompareBranches(leftBranch, rightBranch);
        }
    }
}
