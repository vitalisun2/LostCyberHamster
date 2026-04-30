using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный jump-on-roof action.
    /// </summary>
    internal sealed class JumpOnRoofRetainedActionValidator : IRetainedActionValidator
    {
        private const float _validationEpsilon = 0.0001f;

        /// <summary>
        /// Тип действия, которое умеет сохранять validator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;

        /// <summary>
        /// Проверяет, что сохранённое действие jump-on-roof всё ещё соответствует текущему planning context.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Отсекает неподходящий retained action.
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            // Извлекает данные planning context.
            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            DecisionPoint decisionPoint = context.DecisionPoint;
            ObstacleSnapshot targetObstacle = context.TargetObstacle;
            PlannedAction action = context.Action;

            // Проверяет наличие обязательных данных.
            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || targetObstacle == null
                || action == null)
            {
                return false;
            }

            if (!JumpOnRoofFireWindowFinder.TryGetRoofTarget(
                    planningState.Hamster,
                    decisionPoint.Chain,
                    out ObstacleSnapshot roofObstacle,
                    out int roofWorldIndex,
                    out int roofChainIndex)
                || roofObstacle.InstanceId != targetObstacle.InstanceId)
            {
                return false;
            }

            // Восстанавливает текущий fire shift и начало chain.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    out float fireShift))
            {
                return false;
            }

            // Пересчитывает допустимое chain-окно landing для текущего мира.
            if (!JumpOnRoofFireWindowFinder.TryGetRoofLandingWindow(
                    planningState.Hamster,
                    decisionPoint.Chain,
                    roofObstacle,
                    roofChainIndex,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift,
                    out bool _))
            {
                return false;
            }

            // Проверяет, что fire shift остаётся внутри допустимого окна.
            if (fireShift < firstFireShift - _validationEpsilon
                || fireShift > lastFireShift + _validationEpsilon)
            {
                return false;
            }

            // Сверяет runtime outcome с ожидаемой посадкой на крышу.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return JumpOnRoofFireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                action.PostFireWorldShift,
                roofWorldIndex);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift для retained action в projected-координатах.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            out float fireShift)
        {
            // Проверяет наличие исходных данных.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Пытается найти trigger obstacle по instance id.
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - action.TriggerX;
                    return true;
                }
            }

            // Использует target obstacle как fallback.
            fireShift = targetObstacle.LeftX - action.TriggerX;
            return true;
        }
    }
}
