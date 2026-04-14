using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningBranch
    {
        public PlanningBranch(IReadOnlyList<PlannedAction> actions, PlanningBranchMetrics metrics)
        {
            Actions = actions ?? Array.Empty<PlannedAction>();
            Metrics = metrics ?? PlanningBranchMetrics.Empty;
        }

        public IReadOnlyList<PlannedAction> Actions { get; }
        public PlanningBranchMetrics Metrics { get; }
        public bool HasActions => Actions.Count > 0;
        public int TotalEnergyCost => Metrics.TotalEnergyCost;
        public int ActionCount => Metrics.ActionCount;
        public float FirstTriggerX => Metrics.FirstTriggerX ?? 0f;

        public static PlanningBranch FromLeaf(PlanningGraphNode leafNode)
        {
            var actions = new List<PlannedAction>(leafNode.Metrics.ActionCount);
            for (PlanningGraphNode current = leafNode; current != null && !current.IsRoot; current = current.Parent)
                actions.Add(current.IncomingAction);

            actions.Reverse();
            return new PlanningBranch(actions, leafNode.Metrics);
        }
    }
}