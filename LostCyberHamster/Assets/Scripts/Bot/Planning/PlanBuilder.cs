using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanBuilder
    {
        private readonly PlanningGraphBuilder _graphBuilder;
        private readonly PlanEvaluator _planEvaluator;

        public PlanBuilder(
            ActionGenerator actionGenerator,
            TransitionSimulator transitionSimulator,
            PlanEvaluator planEvaluator)
        {
            _graphBuilder = new PlanningGraphBuilder(actionGenerator, transitionSimulator);
            _planEvaluator = planEvaluator;
        }

        public BotPlan Build(BotPerceptionSnapshot perceptionSnapshot, CommittedPlan committedPlan)
        {
            if (perceptionSnapshot == null)
                return BotPlan.Empty(committedPlan?.CommittedBoundaryX ?? 0f);

            // Expand all reachable branches from the current runtime snapshot.
            IReadOnlyList<PlanningBranch> branches = _graphBuilder.BuildBranches(perceptionSnapshot);

            // Select the best complete branch and convert it back to executable actions.
            PlanningBranch bestBranch = _planEvaluator.SelectBest(branches);
            if (bestBranch == null || !bestBranch.HasActions)
                return BotPlan.Empty(GetCommittedBoundaryX(committedPlan, perceptionSnapshot));

            float score = _planEvaluator.Score(bestBranch);
            return new BotPlan(bestBranch.Actions, perceptionSnapshot.ScreenRightEdgeX, score);
        }

        private static float GetCommittedBoundaryX(CommittedPlan committedPlan, BotPerceptionSnapshot perceptionSnapshot)
        {
            if (committedPlan != null && committedPlan.CommittedBoundaryX > 0f)
                return committedPlan.CommittedBoundaryX;

            return perceptionSnapshot != null ? perceptionSnapshot.ScreenRightEdgeX : 0f;
        }
    }
}
