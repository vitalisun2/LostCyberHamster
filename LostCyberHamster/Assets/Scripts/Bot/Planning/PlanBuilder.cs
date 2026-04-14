using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanBuilder
    {
        private readonly ActionGenerator _actionGenerator;
        private readonly TransitionSimulator _transitionSimulator;
        private readonly PlanEvaluator _planEvaluator;

        public PlanBuilder(
            ActionGenerator actionGenerator,
            TransitionSimulator transitionSimulator,
            PlanEvaluator planEvaluator)
        {
            _actionGenerator = actionGenerator;
            _transitionSimulator = transitionSimulator;
            _planEvaluator = planEvaluator;
        }

        public BotPlan Build(BotPerceptionSnapshot perceptionSnapshot, CommittedPlan committedPlan)
        {
            if (perceptionSnapshot == null)
                return BotPlan.Empty(committedPlan.CommittedBoundaryX);

            var actions = new List<PlannedAction>();
            PlanningState planningState = PlanningState.FromSnapshot(perceptionSnapshot);

            for (int depth = 0; depth < perceptionSnapshot.VisibleObstacles.Count; depth++)
            {
                IReadOnlyList<PlannedAction> candidates = _actionGenerator.Generate(planningState, perceptionSnapshot);
                if (candidates.Count == 0)
                    break;

                PlannedAction bestAction = _planEvaluator.SelectBest(candidates);
                if (bestAction == null)
                    break;

                actions.Add(bestAction);
                planningState = _transitionSimulator.Simulate(planningState, bestAction, perceptionSnapshot);
            }

            float score = _planEvaluator.Score(actions);
            return new BotPlan(actions, perceptionSnapshot.ScreenRightEdgeX, score);
        }
    }
}
