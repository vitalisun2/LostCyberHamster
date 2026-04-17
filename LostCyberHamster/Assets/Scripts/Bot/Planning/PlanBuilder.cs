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

        public BotPlan Build(WorldSnapshot worldSnapshot, BotPlan committedPlan)
        {
            if (worldSnapshot == null)
                return BotPlan.Empty(committedPlan?.CommittedBoundaryX ?? 0f);

            var actions = new List<PlannedAction>();
            PlanningState rootState = PlanningState.FromSnapshot(worldSnapshot);
            PlanningState tailRootState = ProjectCommittedPrefix(
                worldSnapshot,
                committedPlan,
                rootState,
                actions);

            // Expand only the tail beyond the committed prefix.
            IReadOnlyList<PlanningBranch> branches = _graphBuilder.BuildBranches(worldSnapshot, tailRootState);
            PlanningBranch bestBranch = _planEvaluator.SelectBest(branches);

            if (bestBranch != null && bestBranch.HasActions)
            {
                for (int actionIndex = 0; actionIndex < bestBranch.Actions.Count; actionIndex++)
                    actions.Add(bestBranch.Actions[actionIndex]);
            }

            if (actions.Count == 0)
                return BotPlan.Empty(GetCommittedBoundaryX(committedPlan, worldSnapshot));

            float score = _planEvaluator.Score(actions);
            return new BotPlan(actions, worldSnapshot.ScreenRightEdgeX, score);
        }

        private PlanningState ProjectCommittedPrefix(
            WorldSnapshot worldSnapshot,
            BotPlan committedPlan,
            PlanningState rootState,
            List<PlannedAction> retainedActions)
        {
            if (committedPlan == null || !committedPlan.HasActions)
                return rootState;

            PlanningState currentState = rootState;
            IReadOnlyList<PlannedAction> currentActions = committedPlan.Actions;

            for (int actionIndex = 0; actionIndex < currentActions.Count; actionIndex++)
            {
                PlannedAction action = currentActions[actionIndex];
                if (!ShouldRetainAction(action, worldSnapshot))
                    break;

                PlanningState nextState = _transitionSimulator.Simulate(currentState, action, worldSnapshot);
                if (nextState == null)
                    break;

                retainedActions.Add(action);
                currentState = nextState;
            }

            return currentState;
        }

        private static bool ShouldRetainAction(PlannedAction action, WorldSnapshot worldSnapshot)
        {
            return action.RenderWorldX >= worldSnapshot.ScreenLeftEdgeX
                && action.RenderWorldX <= worldSnapshot.ScreenRightEdgeX;
        }

        private static float GetCommittedBoundaryX(BotPlan committedPlan, WorldSnapshot worldSnapshot)
        {
            if (committedPlan != null && committedPlan.CommittedBoundaryX > 0f)
                return committedPlan.CommittedBoundaryX;

            return worldSnapshot != null ? worldSnapshot.ScreenRightEdgeX : 0f;
        }
    }
}
