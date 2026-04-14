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
            return BotPlan.Empty(committedPlan.CommittedBoundaryX);
        }
    }
}
