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

            float commonHorizonProjectionWorldShift = GetCommonSelectionHorizon(candidates);
            PlanningBranch best = candidates[0];
            for (int candidateIndex = 1; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (PlanningBranchComparer.CompareAtCommonHorizon(
                        candidates[candidateIndex],
                        best,
                        commonHorizonProjectionWorldShift) < 0)
                {
                    best = candidates[candidateIndex];
                }
            }

            return best;
        }

        /// <summary>
        /// Выбирает dead-end ветку с самым дальним первым провалом.
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

        private static float GetCommonSelectionHorizon(IReadOnlyList<PlanningBranch> candidates)
        {
            bool hasCandidate = false;
            float commonHorizonProjectionWorldShift = 0f;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                PlanningBranch candidate = candidates[candidateIndex];
                if (candidate == null)
                    continue;

                if (hasCandidate
                    && candidate.FinalProjectionWorldShift >= commonHorizonProjectionWorldShift)
                {
                    continue;
                }

                hasCandidate = true;
                commonHorizonProjectionWorldShift = candidate.FinalProjectionWorldShift;
            }

            return commonHorizonProjectionWorldShift;
        }

        /// <summary>
        /// Сравнивает dead-end ветки по индексу первого провала: чем дальше провал от корня, тем лучше.
        /// </summary>
        private static int CompareDeadEndBranches(PlanningDeadEndBranch left, PlanningDeadEndBranch right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left?.Branch == null)
                return 1;

            if (right?.Branch == null)
                return -1;

            int leftFailureDepth = GetDeadEndFailureDepth(left);
            int rightFailureDepth = GetDeadEndFailureDepth(right);
            return rightFailureDepth.CompareTo(leftFailureDepth);
        }

        /// <summary>
        /// Возвращает глубину первого unresolved dead-end для ветки.
        /// </summary>
        private static int GetDeadEndFailureDepth(PlanningDeadEndBranch deadEndBranch)
        {
            return deadEndBranch?.Report?.Depth ?? 0;
        }
    }
}
