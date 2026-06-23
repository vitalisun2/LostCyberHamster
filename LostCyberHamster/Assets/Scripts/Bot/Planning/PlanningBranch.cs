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
            InitialProjectionWorldShift = CalculateInitialProjectionWorldShift(Actions, finalProjectionWorldShift);
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
        private float InitialProjectionWorldShift { get; }

        /// <summary>
        /// Собирает ветку из листового узла графа планирования.
        /// </summary>
        public static PlanningBranch FromLeaf(PlanningGraphNode leafNode)
        {
            return new PlanningBranch(
                leafNode.Actions,
                leafNode.Metrics,
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

            int actionCountToHorizon = 0;
            float currentProjectionWorldShift = InitialProjectionWorldShift;
            for (int actionIndex = 0; actionIndex < Actions.Count; actionIndex++)
            {
                if (currentProjectionWorldShift >= horizonProjectionWorldShift - horizonEpsilon)
                    break;

                actionCountToHorizon++;
                PlannedAction action = Actions[actionIndex];
                if (action != null)
                    currentProjectionWorldShift += action.CompletionWorldShift;
            }

            return PlanningBranchMetrics.FromActionPrefix(Actions, actionCountToHorizon);
        }

        private static float CalculateInitialProjectionWorldShift(
            IReadOnlyList<PlannedAction> actions,
            float finalProjectionWorldShift)
        {
            float projectionWorldShift = finalProjectionWorldShift;
            if (actions == null)
                return projectionWorldShift;

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                PlannedAction action = actions[actionIndex];
                if (action != null)
                    projectionWorldShift -= action.CompletionWorldShift;
            }

            return projectionWorldShift;
        }
    }
}
