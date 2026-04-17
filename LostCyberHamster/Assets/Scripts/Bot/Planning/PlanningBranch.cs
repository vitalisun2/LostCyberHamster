using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Представляет одну ветку дерева решений как последовательность действий.
    /// </summary>
    public sealed class PlanningBranch
    {
        /// <summary>
        /// Создает ветку из списка действий и накопленных метрик.
        /// </summary>
        public PlanningBranch(
            IReadOnlyList<PlannedAction> actions,
            PlanningBranchMetrics metrics,
            int finalNextObstacleIndex,
            float finalProjectionWorldShift)
        {
            Actions = actions ?? Array.Empty<PlannedAction>();
            Metrics = metrics ?? PlanningBranchMetrics.Empty;
            FinalNextObstacleIndex = finalNextObstacleIndex;
            FinalProjectionWorldShift = finalProjectionWorldShift;
        }

        public IReadOnlyList<PlannedAction> Actions { get; }
        public PlanningBranchMetrics Metrics { get; }
        public int FinalNextObstacleIndex { get; }
        public float FinalProjectionWorldShift { get; }
        public bool HasActions => Actions.Count > 0;
        public int TotalEnergyCost => Metrics.TotalEnergyCost;
        public int TapCount => Metrics.TapCount;
        public int ActionCount => Metrics.ActionCount;
        public float FirstTriggerX => Metrics.FirstTriggerX ?? 0f;

        /// <summary>
        /// Собирает ветку из листового узла графа планирования.
        /// </summary>
        public static PlanningBranch FromLeaf(PlanningGraphNode leafNode)
        {
            var actions = new List<PlannedAction>(leafNode.Metrics.ActionCount);
            for (PlanningGraphNode current = leafNode; current != null && !current.IsRoot; current = current.Parent)
                actions.Add(current.IncomingAction);

            actions.Reverse();
            return new PlanningBranch(
                actions,
                leafNode.Metrics,
                leafNode.State.NextObstacleIndex,
                leafNode.State.ProjectionWorldShift);
        }
    }
}
