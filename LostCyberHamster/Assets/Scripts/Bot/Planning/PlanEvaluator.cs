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
        /// Выбирает dead-end ветку с максимальным безопасным продвижением.
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
                - totalEnergyCost * 100f
                - tapCount * 10f;
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

            return 0;
        }

        /// <summary>
        /// Сравнивает dead-end ветки, предпочитая короткий осмысленный fallback вместо длинного switch-каскада.
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

            bool leftHasNonSwitchAction = HasNonSwitchAction(leftBranch);
            bool rightHasNonSwitchAction = HasNonSwitchAction(rightBranch);
            if (leftHasNonSwitchAction != rightHasNonSwitchAction)
                return leftHasNonSwitchAction ? -1 : 1;

            int compare = CountSwitchActions(leftBranch).CompareTo(CountSwitchActions(rightBranch));
            if (compare != 0)
                return compare;

            compare = leftBranch.ActionCount.CompareTo(rightBranch.ActionCount);
            if (compare != 0)
                return compare;

            compare = rightBranch.FinalNextObstacleIndex.CompareTo(leftBranch.FinalNextObstacleIndex);
            if (compare != 0)
                return compare;

            compare = rightBranch.FinalProjectionWorldShift.CompareTo(leftBranch.FinalProjectionWorldShift);
            if (compare != 0)
                return compare;

            return CompareBranches(leftBranch, rightBranch);
        }

        private static bool HasNonSwitchAction(PlanningBranch branch)
        {
            if (branch?.Actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < branch.Actions.Count; actionIndex++)
            {
                PlannedAction action = branch.Actions[actionIndex];
                if (action != null && !IsSwitchAction(action.Kind))
                    return true;
            }

            return false;
        }

        private static int CountSwitchActions(PlanningBranch branch)
        {
            if (branch?.Actions == null)
                return 0;

            int count = 0;
            for (int actionIndex = 0; actionIndex < branch.Actions.Count; actionIndex++)
            {
                PlannedAction action = branch.Actions[actionIndex];
                if (action != null && IsSwitchAction(action.Kind))
                    count++;
            }

            return count;
        }

        private static bool IsSwitchAction(BotActionKind kind)
        {
            return kind == BotActionKind.SwitchLane
                || kind == BotActionKind.RoofSwitchLaneExit;
        }
    }
}
