using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningGraphNode
    {
        private PlanningGraphNode(
            PlanningState state,
            PlanningGraphNode parent,
            PlannedAction incomingAction,
            int depth,
            PlanningBranchMetrics metrics)
        {
            State = state;
            Parent = parent;
            IncomingAction = incomingAction;
            Depth = depth;
            Metrics = metrics;
            StateKey = PlanningStateKey.FromState(state);
        }

        public PlanningState State { get; }
        public PlanningGraphNode Parent { get; }
        public PlannedAction IncomingAction { get; }
        public int Depth { get; }
        public PlanningBranchMetrics Metrics { get; }
        internal PlanningStateKey StateKey { get; }
        public bool IsRoot => Parent == null;

        public static PlanningGraphNode CreateRoot(PlanningState rootState)
        {
            return new PlanningGraphNode(rootState, null, null, depth: 0, PlanningBranchMetrics.Empty);
        }

        public PlanningGraphNode CreateChild(PlanningState childState, PlannedAction action)
        {
            return new PlanningGraphNode(
                childState,
                this,
                action,
                Depth + 1,
                Metrics.Append(action));
        }
    }
}