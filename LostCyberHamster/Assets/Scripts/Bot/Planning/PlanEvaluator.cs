using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanEvaluator
    {
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

        public float Score(IReadOnlyList<PlannedAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return 0f;

            int totalEnergyCost = 0;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                totalEnergyCost += actions[actionIndex].EnergyCost;

            return actions.Count * 100f
                - totalEnergyCost * 10f
                - actions[0].TriggerX;
        }

        private static int CompareBranches(PlanningBranch left, PlanningBranch right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            int compare = left.TotalEnergyCost.CompareTo(right.TotalEnergyCost);
            if (compare != 0)
                return compare;

            compare = right.ActionCount.CompareTo(left.ActionCount);
            if (compare != 0)
                return compare;

            compare = left.FirstTriggerX.CompareTo(right.FirstTriggerX);
            if (compare != 0)
                return compare;

            return CompareActionSequences(left.Actions, right.Actions);
        }

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
