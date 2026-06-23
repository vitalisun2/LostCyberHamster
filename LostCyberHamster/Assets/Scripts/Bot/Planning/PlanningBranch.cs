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
        public int EnergyCost => Metrics.EnergyCost;
        public int EnergyBeforeFirstMajor => Metrics.EnergyBeforeFirstMajor;
        public int ActionCount => Metrics.ActionCount;
        public int MajorObjectiveCount => Metrics.MajorObjectiveCount;
        public int LifeCollectibleValue => Metrics.LifeCollectibleValue;
        public int EnergyCollectibleValue => Metrics.EnergyCollectibleValue;
        public int CrystalCollectibleValue => Metrics.CrystalCollectibleValue;
        public int CoinCollectibleValue => Metrics.CoinCollectibleValue;

        /// <summary>
        /// Собирает ветку из листового узла графа планирования.
        /// </summary>
        public static PlanningBranch FromLeaf(PlanningGraphNode leafNode)
        {
            var actions = new List<PlannedAction>(leafNode.Depth);
            for (PlanningGraphNode current = leafNode; current != null && !current.IsRoot; current = current.Parent)
                actions.Add(current.IncomingAction);

            actions.Reverse();
            return new PlanningBranch(
                actions,
                PlanningBranchMetrics.FromActions(actions),
                leafNode.State.NextObstacleIndex,
                leafNode.State.ProjectionWorldShift);
        }

        /// <summary>
        /// Пересчитывает метрики действий, необходимых для достижения общего горизонта сравнения,
        /// чтобы более длинная ветка не штрафовалась за будущие действия за этим горизонтом.
        /// </summary>
        public PlanningBranchMetrics GetMetricsToReach(float horizonProjectionWorldShift)
        {
            const float horizonEpsilon = 0.001f;

            if (Actions.Count == 0)
                return PlanningBranchMetrics.Empty;

            float currentProjectionWorldShift = GetInitialProjectionWorldShift();
            var actionsToHorizon = new List<PlannedAction>(Actions.Count);
            for (int actionIndex = 0; actionIndex < Actions.Count; actionIndex++)
            {
                PlannedAction action = Actions[actionIndex];
                if (action == null)
                    continue;

                if (currentProjectionWorldShift >= horizonProjectionWorldShift - horizonEpsilon)
                    break;

                actionsToHorizon.Add(action);
                currentProjectionWorldShift += action.CompletionWorldShift;
            }

            return PlanningBranchMetrics.FromActions(actionsToHorizon);
        }

        private float GetInitialProjectionWorldShift()
        {
            float projectionWorldShift = FinalProjectionWorldShift;
            for (int actionIndex = 0; actionIndex < Actions.Count; actionIndex++)
            {
                PlannedAction action = Actions[actionIndex];
                if (action != null)
                    projectionWorldShift -= action.CompletionWorldShift;
            }

            return projectionWorldShift;
        }
    }
}
