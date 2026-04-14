using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanBuilder
    {
        private readonly PlanningGraphBuilder _graphBuilder;
        private readonly TransitionSimulator _transitionSimulator;
        private readonly PlanEvaluator _planEvaluator;

        public PlanBuilder(
            ActionGenerator actionGenerator,
            TransitionSimulator transitionSimulator,
            PlanEvaluator planEvaluator)
        {
            _graphBuilder = new PlanningGraphBuilder(actionGenerator, transitionSimulator);
            _transitionSimulator = transitionSimulator;
            _planEvaluator = planEvaluator;
        }

        public BotPlan Build(BotPerceptionSnapshot perceptionSnapshot, CommittedPlan committedPlan)
        {
            if (perceptionSnapshot == null)
                return BotPlan.Empty(committedPlan?.CommittedBoundaryX ?? 0f);

            var actions = new List<PlannedAction>();
            PlanningState rootState = PlanningState.FromSnapshot(perceptionSnapshot);
            PlanningState tailRootState = ProjectCommittedPrefix(
                perceptionSnapshot,
                committedPlan,
                rootState,
                actions);

            // Expand only the tail beyond the committed prefix.
            IReadOnlyList<PlanningBranch> branches = _graphBuilder.BuildBranches(perceptionSnapshot, tailRootState);
            PlanningBranch bestBranch = _planEvaluator.SelectBest(branches);

            if (bestBranch != null && bestBranch.HasActions)
            {
                for (int actionIndex = 0; actionIndex < bestBranch.Actions.Count; actionIndex++)
                    actions.Add(bestBranch.Actions[actionIndex]);
            }

            if (actions.Count == 0)
                return BotPlan.Empty(GetCommittedBoundaryX(committedPlan, perceptionSnapshot));

            float score = _planEvaluator.Score(actions);
            return new BotPlan(actions, perceptionSnapshot.ScreenRightEdgeX, score);
        }

        private PlanningState ProjectCommittedPrefix(
            BotPerceptionSnapshot perceptionSnapshot,
            CommittedPlan committedPlan,
            PlanningState rootState,
            List<PlannedAction> retainedActions)
        {
            if (committedPlan?.Current == null || !committedPlan.Current.HasActions)
                return rootState;

            PlanningState currentState = rootState;
            IReadOnlyList<PlannedAction> currentActions = committedPlan.Current.Actions;

            for (int actionIndex = 0; actionIndex < currentActions.Count; actionIndex++)
            {
                PlannedAction action = currentActions[actionIndex];
                if (!ShouldRetainAction(action, perceptionSnapshot))
                    break;

                PlanningState nextState = _transitionSimulator.Simulate(currentState, action, perceptionSnapshot);
                if (nextState == null)
                    break;

                retainedActions.Add(action);
                currentState = nextState;
            }

            return currentState;
        }

        private static bool ShouldRetainAction(PlannedAction action, BotPerceptionSnapshot perceptionSnapshot)
        {
            return action.RenderWorldX >= perceptionSnapshot.ScreenLeftEdgeX
                && action.RenderWorldX <= perceptionSnapshot.ScreenRightEdgeX;
        }

        private static float GetCommittedBoundaryX(CommittedPlan committedPlan, BotPerceptionSnapshot perceptionSnapshot)
        {
            if (committedPlan != null && committedPlan.CommittedBoundaryX > 0f)
                return committedPlan.CommittedBoundaryX;

            return perceptionSnapshot != null ? perceptionSnapshot.ScreenRightEdgeX : 0f;
        }
    }
}
