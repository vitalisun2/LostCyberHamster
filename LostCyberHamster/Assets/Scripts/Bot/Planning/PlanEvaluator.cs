using System;
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
                if (actions[actionIndex].Kind == BotActionKind.SwitchLane)
                    tapCount++;
            }

            return 1000f
                - totalEnergyCost * 100f
                - tapCount * 10f
                - actions[0].TriggerX;
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

            int compare = left.Metrics.CompareJumpOnObjectivePriority(right.Metrics);
            if (compare != 0)
                return compare;

            compare = left.TotalEnergyCost.CompareTo(right.TotalEnergyCost);
            if (compare != 0)
                return compare;

            compare = left.TapCount.CompareTo(right.TapCount);
            if (compare != 0)
                return compare;

            compare = CompareFirstTriggerForSameJumpOnObjective(left, right);
            if (compare != 0)
                return compare;

            compare = right.FinalNextObstacleIndex.CompareTo(left.FinalNextObstacleIndex);
            if (compare != 0)
                return compare;

            compare = right.FinalProjectionWorldShift.CompareTo(left.FinalProjectionWorldShift);
            if (compare != 0)
                return compare;

            compare = left.FirstTriggerX.CompareTo(right.FirstTriggerX);
            if (compare != 0)
                return compare;

            return CompareActionSequences(left.Actions, right.Actions);
        }

        /// <summary>
        /// Для веток с одинаковым jump-on objective предпочитает более ранний первый trigger.
        /// </summary>
        private static int CompareFirstTriggerForSameJumpOnObjective(
            PlanningBranch left,
            PlanningBranch right)
        {
            int? leftTargetIndex = left.FirstJumpOnObjectiveTargetIndex;
            int? rightTargetIndex = right.FirstJumpOnObjectiveTargetIndex;
            if (!leftTargetIndex.HasValue
                || !rightTargetIndex.HasValue
                || leftTargetIndex.Value != rightTargetIndex.Value)
            {
                return 0;
            }

            return right.FirstTriggerX.CompareTo(left.FirstTriggerX);
        }

        /// <summary>
        /// Сравнивает последовательности действий.
        /// </summary>
        private static int CompareActionSequences(IReadOnlyList<PlannedAction> left, IReadOnlyList<PlannedAction> right)
        {
            int actionCount = Math.Min(left.Count, right.Count);
            for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                PlannedAction leftAction = left[actionIndex];
                PlannedAction rightAction = right[actionIndex];

                int compare = leftAction.EnergyCost.CompareTo(rightAction.EnergyCost);
                if (compare != 0)
                    return compare;

                compare = leftAction.TriggerX.CompareTo(rightAction.TriggerX);
                if (compare != 0)
                    return compare;

                compare = leftAction.TargetObstacleIndex.CompareTo(rightAction.TargetObstacleIndex);
                if (compare != 0)
                    return compare;
            }

            return left.Count.CompareTo(right.Count);
        }
    }
}
