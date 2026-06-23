using System;
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
            IReadOnlyList<PlannedAction> actions,
            PlanningBranchMetrics metrics)
        {
            State = state;
            Parent = parent;
            IncomingAction = incomingAction;
            Depth = depth;
            Actions = actions ?? Array.Empty<PlannedAction>();
            Metrics = metrics;
            StateKey = PlanningStateKey.FromState(state);
        }

        public PlanningState State { get; }
        public PlanningGraphNode Parent { get; }
        public PlannedAction IncomingAction { get; }
        public int Depth { get; }
        public IReadOnlyList<PlannedAction> Actions { get; }
        public PlanningBranchMetrics Metrics { get; }
        internal PlanningStateKey StateKey { get; }
        public bool IsRoot => Parent == null;

        /// <summary>
        /// Создает корневой узел графа для заданного planning-состояния.
        /// </summary>
        public static PlanningGraphNode CreateRoot(PlanningState rootState)
        {
            return new PlanningGraphNode(
                rootState,
                null,
                null,
                depth: 0,
                Array.Empty<PlannedAction>(),
                PlanningBranchMetrics.Empty);
        }

        /// <summary>
        /// Создает дочерний узел после выполнения одного действия.
        /// </summary>
        public PlanningGraphNode CreateChild(PlanningState childState, PlannedAction action)
        {
            IReadOnlyList<PlannedAction> actions = AppendAction(Actions, action);
            return new PlanningGraphNode(
                childState,
                this,
                action,
                Depth + 1,
                actions,
                PlanningBranchMetrics.FromActions(actions));
        }

        private static IReadOnlyList<PlannedAction> AppendAction(
            IReadOnlyList<PlannedAction> prefix,
            PlannedAction action)
        {
            int prefixCount = prefix?.Count ?? 0;
            var actions = new PlannedAction[prefixCount + 1];
            for (int actionIndex = 0; actionIndex < prefixCount; actionIndex++)
                actions[actionIndex] = prefix[actionIndex];

            actions[prefixCount] = action;
            return actions;
        }
    }
}
