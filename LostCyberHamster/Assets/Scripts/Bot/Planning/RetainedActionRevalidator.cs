using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Проверяет, можно ли сохранить пограничное committed-действие на новом snapshot мира.
    /// </summary>
    public sealed class RetainedActionRevalidator
    {
        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();
        private readonly IReadOnlyDictionary<BotActionKind, IRetainedActionValidator> _validatorsByKind;

        internal RetainedActionRevalidator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            var validatorsByKind = new Dictionary<BotActionKind, IRetainedActionValidator>();
            for (int strategyIndex = 0; strategyIndex < strategies?.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy?.RetainedValidator == null)
                    continue;

                validatorsByKind.Add(strategy.ActionKind, strategy.RetainedValidator);
            }

            _validatorsByKind = validatorsByKind;
        }

        /// <summary>
        /// Возвращает true, если последнее retained-действие всё ещё безопасно и остаётся актуальным.
        /// </summary>
        public bool IsStillValid(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return false;

            // Сначала проецируем текущий мир в состояние прямо перед boundary-действием.
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return false;

            // Затем убеждаемся, что действие всё ещё направлено в актуальный decision point.
            if (!TryFindActionTarget(projectedWorldSnapshot, action, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return false;

            bool foundTargetDecisionPoint =
                _decisionPointDetector.TryDetectDecisionPointForRetainedTarget(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    out DecisionPoint decisionPoint);
            if (!foundTargetDecisionPoint
                && !_decisionPointDetector.TryDetectRequiredDecisionPoint(planningState, projectedWorldSnapshot, out decisionPoint))
            {
                return false;
            }

            if (!foundTargetDecisionPoint
                && !decisionPoint.Chain.ContainsObstacle(targetObstacle)
                && !CanTargetLiveOutsideDecisionChain(action))
            {
                return false;
            }

            if (!_validatorsByKind.TryGetValue(action.Kind, out IRetainedActionValidator validator))
                return false;

            return validator.IsStillValid(new RetainedActionContext(
                planningState,
                projectedWorldSnapshot,
                decisionPoint,
                targetObstacle,
                targetObstacleIndex,
                action));
        }

        /// <summary>
        /// Возвращает true для actions, у которых target может лежать дальше текущего blocker chain.
        /// </summary>
        private static bool CanTargetLiveOutsideDecisionChain(PlannedAction action)
        {
            return action.Kind == BotActionKind.JumpOn
                || action.Kind == BotActionKind.SuperJumpOn
                || action.Kind == BotActionKind.JumpOnFromRoof
                || action.Kind == BotActionKind.SuperJumpOnFromRoof
                || action.Kind == BotActionKind.JumpFromRoofOnRoof
                || action.Kind == BotActionKind.SuperJumpFromRoofOnRoof;
        }

        /// <summary>
        /// Находит целевое obstacle для retained-action.
        /// </summary>
        private static bool TryFindActionTarget(
            WorldSnapshot projectedWorldSnapshot,
            PlannedAction action,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;

            if (projectedWorldSnapshot == null || action == null)
                return false;

            if (action.TargetObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != action.TargetObstacleInstanceId.Value)
                        continue;

                    targetObstacle = obstacle;
                    targetObstacleIndex = obstacleIndex;
                    return true;
                }
            }

            if (action.TargetObstacleIndex < 0 || action.TargetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count)
                return false;

            targetObstacleIndex = action.TargetObstacleIndex;
            targetObstacle = projectedWorldSnapshot.Obstacles[targetObstacleIndex];
            return true;
        }
    }
}
