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
    /// Генерирует candidate-действия бота для текущего planning-состояния через доступные planning-стратегии.
    /// </summary>
    public sealed class ActionGenerator
    {
        /// <summary>
        /// Список стратегий, которые предлагают действия для найденной точки решения.
        /// </summary>
        private readonly IReadOnlyList<IPlanningStrategy> _strategies;

        /// <summary>
        /// Детектор обязательных угроз и дополнительных jump-on opportunities.
        /// </summary>
        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();

        internal ActionGenerator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategy>();
        }

        /// <summary>
        /// Генерирует список действий, доступных из текущего planning-состояния и snapshot мира.
        /// </summary>
        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            // Проверяет входные данные.
            var plannedActions = new List<PlannedAction>();
            if (planningState == null || worldSnapshot == null)
                return plannedActions;

            // Проецирует мир в состояние планирования.
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);

            // Собирает действия для обязательной угрозы.
            DecisionPoint blockingDecisionPoint = null;
            if (_decisionPointDetector.TryDetectBlockingThreat(
                    planningState,
                    projectedWorldSnapshot,
                    out blockingDecisionPoint))
            {
                CollectActionsForDecisionPoint(
                    planningState,
                    projectedWorldSnapshot,
                    blockingDecisionPoint,
                    plannedActions);
            }

            // Собирает действия для optional jump-on opportunity.
            DecisionPoint opportunityDecisionPoint = null;
            if (_decisionPointDetector.TryDetectJumpOnOpportunity(
                    planningState,
                    projectedWorldSnapshot,
                    out opportunityDecisionPoint))
            {
                CollectActionsForDecisionPoint(
                    planningState,
                    projectedWorldSnapshot,
                    opportunityDecisionPoint,
                    plannedActions);
            }

            // Проверяет наличие точки решения.
            DecisionPoint logDecisionPoint = blockingDecisionPoint ?? opportunityDecisionPoint;
            if (logDecisionPoint == null)
            {
                LogNoDecisionPoint(planningState, projectedWorldSnapshot);
                return plannedActions;
            }

            // Убирает избыточные super jump-on варианты.
            RemoveSuperJumpOnCandidatesCoveredByOrdinaryJumpOn(plannedActions);

            // Логирует отсутствие подходящих действий.
            if (plannedActions.Count == 0)
            {
                DebugManager.DiagLogVerbose(
                    $"[Bot PLAN] NO_ACTIONS obstacle={logDecisionPoint.Obstacle.ObstacleType} " +
                    $"kind={logDecisionPoint.Kind} " +
                    $"leftX={logDecisionPoint.Obstacle.LeftX:F2} rightX={logDecisionPoint.Obstacle.RightX:F2} " +
                    $"lane={(logDecisionPoint.Obstacle.IsBottomLine ? "bottom" : "top")} " +
                    $"projection={planningState.ProjectionWorldShift:F2} " +
                    $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
            }

            return plannedActions;
        }

        /// <summary>
        /// Запрашивает у всех planning-стратегий действия для указанной точки решения.
        /// </summary>
        private void CollectActionsForDecisionPoint(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> plannedActions)
        {
            // Обходит стратегии в порядке их приоритета.
            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                _strategies[strategyIndex].CollectActions(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint,
                    plannedActions);
            }
        }

        /// <summary>
        /// Удаляет super jump-on кандидаты, если тот же target уже покрыт обычным jump-on.
        /// </summary>
        private static void RemoveSuperJumpOnCandidatesCoveredByOrdinaryJumpOn(List<PlannedAction> plannedActions)
        {
            // Проверяет, есть ли что фильтровать.
            if (plannedActions == null || plannedActions.Count < 2)
                return;

            // Удаляет покрытые super-кандидаты с конца списка.
            for (int actionIndex = plannedActions.Count - 1; actionIndex >= 0; actionIndex--)
            {
                PlannedAction action = plannedActions[actionIndex];
                if (action == null || !TryGetOrdinaryJumpOnKind(action.Kind, out BotActionKind ordinaryKind))
                    continue;

                if (HasOrdinaryJumpOnCandidateForSameTarget(plannedActions, action, ordinaryKind))
                    plannedActions.RemoveAt(actionIndex);
            }
        }

        /// <summary>
        /// Проверяет наличие ordinary jump-on кандидата для того же target, что и у super-действия.
        /// </summary>
        private static bool HasOrdinaryJumpOnCandidateForSameTarget(
            IReadOnlyList<PlannedAction> plannedActions,
            PlannedAction superJumpOnAction,
            BotActionKind ordinaryKind)
        {
            // Ищет соответствующий ordinary action среди кандидатов.
            for (int actionIndex = 0; actionIndex < plannedActions.Count; actionIndex++)
            {
                PlannedAction action = plannedActions[actionIndex];
                if (action == null || action.Kind != ordinaryKind)
                    continue;

                if (TargetsSameObstacle(action, superJumpOnAction))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает ordinary-вариант для super jump-on action.
        /// </summary>
        private static bool TryGetOrdinaryJumpOnKind(
            BotActionKind superKind,
            out BotActionKind ordinaryKind)
        {
            // Сопоставляет пары ordinary/super jump-on.
            if (superKind == BotActionKind.SuperJumpOn)
            {
                ordinaryKind = BotActionKind.JumpOn;
                return true;
            }

            if (superKind == BotActionKind.SuperJumpOnFromRoof)
            {
                ordinaryKind = BotActionKind.JumpOnFromRoof;
                return true;
            }

            ordinaryKind = BotActionKind.None;
            return false;
        }

        /// <summary>
        /// Проверяет, ссылаются ли два действия на один и тот же obstacle target.
        /// </summary>
        private static bool TargetsSameObstacle(PlannedAction left, PlannedAction right)
        {
            // Сравнивает стабильные instance id, если они доступны.
            if (left.TargetObstacleInstanceId.HasValue && right.TargetObstacleInstanceId.HasValue)
                return left.TargetObstacleInstanceId.Value == right.TargetObstacleInstanceId.Value;

            // Использует индекс obstacle как запасной идентификатор.
            return left.TargetObstacleIndex == right.TargetObstacleIndex;
        }

        /// <summary>
        /// Логирует ближайший same-lane obstacle, если detector не нашёл точку решения.
        /// </summary>
        private static void LogNoDecisionPoint(PlanningState planningState, WorldSnapshot projectedWorldSnapshot)
        {
            // Проверяет входные данные.
            if (planningState == null || projectedWorldSnapshot == null)
                return;

            // Ищет ближайший obstacle впереди на текущей линии.
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
