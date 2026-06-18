using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Выбирает лучшую planning-ветку и считает итоговый score плана.
    /// </summary>
    public sealed class PlanEvaluator
    {
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
            int tapCount = 0;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                totalEnergyCost += actions[actionIndex].EnergyCost;
                if (BotActionKindRules.ConsumesTap(actions[actionIndex].Kind))
                    tapCount++;
            }

            return 1000f
                + GetCollectibleScore(actions)
                - totalEnergyCost * 100f
                - tapCount * 10f;
        }

        private static float GetCollectibleScore(IReadOnlyList<PlannedAction> actions)
        {
            float score = 0f;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                PlannedAction action = actions[actionIndex];
                if (action == null || !action.FulfillsCollectibleObjective)
                    continue;

                score += action.CollectibleObjectiveValue.Kind switch
                {
                    CollectibleKind.Life => action.CollectibleObjectiveValue.EffectiveGain * 10000f,
                    CollectibleKind.Energy => action.CollectibleObjectiveValue.IsCriticalEnergy
                        ? action.CollectibleObjectiveValue.EffectiveGain * 1000f
                        : action.CollectibleObjectiveValue.EffectiveGain * 100f,
                    CollectibleKind.Crystal => action.CollectibleObjectiveValue.EffectiveGain * 10f,
                    CollectibleKind.Coin => action.CollectibleObjectiveValue.EffectiveGain,
                    _ => 0f
                };
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

            int compare = left.Metrics.CompareObjectivePriority(right.Metrics);
            if (compare != 0)
                return compare;

            compare = left.TotalEnergyCost.CompareTo(right.TotalEnergyCost);
            if (compare != 0)
                return compare;

            compare = left.TapCount.CompareTo(right.TapCount);
            if (compare != 0)
                return compare;

            return 0;
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
