using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Представляет один узел дерева решений планировщика.
    /// </summary>
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

        /// <summary>
        /// Создает корневой узел графа для заданного planning-состояния.
        /// </summary>
        public static PlanningGraphNode CreateRoot(PlanningState rootState)
        {
            return new PlanningGraphNode(rootState, null, null, depth: 0, PlanningBranchMetrics.Empty);
        }

        /// <summary>
        /// Создает дочерний узел после выполнения одного действия.
        /// </summary>
        public PlanningGraphNode CreateChild(PlanningState childState, PlannedAction action)
        {
            return new PlanningGraphNode(
                childState,
                this,
                action,
                Depth + 1,
                PlanningBranchMetrics.FromActions(BuildActionPrefix(action)));
        }

        private IReadOnlyList<PlannedAction> BuildActionPrefix(PlannedAction action)
        {
            var actions = new List<PlannedAction>(Depth + 1);
            for (PlanningGraphNode current = this; current != null && !current.IsRoot; current = current.Parent)
                actions.Add(current.IncomingAction);

            actions.Reverse();
            actions.Add(action);
            return actions;
        }
    }
}
