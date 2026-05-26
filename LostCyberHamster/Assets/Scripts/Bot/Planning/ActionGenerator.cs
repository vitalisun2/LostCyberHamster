using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Собирает все действия, доступные для текущей точки решения.
    /// </summary>
    public sealed class ActionGenerator
    {
        private readonly IReadOnlyList<IPlanningStrategy> _strategies;
        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();

        /// <summary>
        /// Создает генератор действий поверх набора planning-стратегий.
        /// </summary>
        internal ActionGenerator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategy>();
        }

        /// <summary>
        /// Генерирует кандидатов действий для текущего planning-состояния.
        /// </summary>
        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            var plannedActions = new List<PlannedAction>();
            if (planningState == null || worldSnapshot == null)
                return plannedActions;

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (!_decisionPointDetector.TryDetect(planningState, projectedWorldSnapshot, out DecisionPoint decisionPoint))
            {
                LogNoDecisionPoint(planningState, projectedWorldSnapshot);
                return plannedActions;
            }

            CollectActionsForDecisionPoint(
                planningState,
                projectedWorldSnapshot,
                decisionPoint,
                plannedActions);

            if (!decisionPoint.IsJumpOnOpportunity
                && _decisionPointDetector.TryDetectJumpOnOpportunity(
                    planningState,
                    projectedWorldSnapshot,
                    out DecisionPoint opportunityDecisionPoint))
            {
                CollectActionsForDecisionPoint(
                    planningState,
                    projectedWorldSnapshot,
                    opportunityDecisionPoint,
                    plannedActions);
            }

            RemoveSuperJumpOnCandidatesCoveredByJumpOn(plannedActions);

            if (plannedActions.Count == 0)
            {
                DebugManager.DiagLogVerbose(
                    $"[Bot PLAN] NO_ACTIONS obstacle={decisionPoint.Obstacle.ObstacleType} " +
                    $"kind={decisionPoint.Kind} " +
                    $"leftX={decisionPoint.Obstacle.LeftX:F2} rightX={decisionPoint.Obstacle.RightX:F2} " +
                    $"lane={(decisionPoint.Obstacle.IsBottomLine ? "bottom" : "top")} " +
                    $"projection={planningState.ProjectionWorldShift:F2} " +
                    $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
            }

            return plannedActions;
        }

        private void CollectActionsForDecisionPoint(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> plannedActions)
        {
            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                _strategies[strategyIndex].CollectActions(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint,
                    plannedActions);
            }
        }

        private static void RemoveSuperJumpOnCandidatesCoveredByJumpOn(List<PlannedAction> plannedActions)
        {
            if (plannedActions == null || plannedActions.Count < 2)
                return;

            for (int actionIndex = plannedActions.Count - 1; actionIndex >= 0; actionIndex--)
            {
                PlannedAction action = plannedActions[actionIndex];
                if (action == null || action.Kind != BotActionKind.SuperJumpOn)
                    continue;

                if (HasJumpOnCandidateForSameTarget(plannedActions, action))
                    plannedActions.RemoveAt(actionIndex);
            }
        }

        private static bool HasJumpOnCandidateForSameTarget(
            IReadOnlyList<PlannedAction> plannedActions,
            PlannedAction superJumpOnAction)
        {
            for (int actionIndex = 0; actionIndex < plannedActions.Count; actionIndex++)
            {
                PlannedAction action = plannedActions[actionIndex];
                if (action == null || action.Kind != BotActionKind.JumpOn)
                    continue;

                if (TargetsSameObstacle(action, superJumpOnAction))
                    return true;
            }

            return false;
        }

        private static bool TargetsSameObstacle(PlannedAction left, PlannedAction right)
        {
            if (left.TargetObstacleInstanceId.HasValue && right.TargetObstacleInstanceId.HasValue)
                return left.TargetObstacleInstanceId.Value == right.TargetObstacleInstanceId.Value;

            return left.TargetObstacleIndex == right.TargetObstacleIndex;
        }

        private static void LogNoDecisionPoint(PlanningState planningState, WorldSnapshot projectedWorldSnapshot)
        {
            if (planningState == null || projectedWorldSnapshot == null)
                return;

            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                    continue;

                DebugManager.DiagLogVerbose(
                    $"[Bot PLAN] NO_DECISION nextSameLane={obstacle.ObstacleType} " +
                    $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2} " +
                    $"lane={(obstacle.IsBottomLine ? "bottom" : "top")} " +
                    $"projection={planningState.ProjectionWorldShift:F2}");
                return;
            }
        }
    }
}
