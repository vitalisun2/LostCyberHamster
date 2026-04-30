using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный super-jump-on-roof action.
    /// </summary>
    internal sealed class SuperJumpOnRoofRetainedActionValidator : IRetainedActionValidator
    {
        private const float _validationEpsilon = 0.0001f;

        /// <summary>
        /// Тип действия, которое умеет сохранять validator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpOnRoof;

        /// <summary>
        /// Проверяет, что сохранённое действие super-jump-on-roof всё ещё соответствует текущему planning context.
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

            if (!SuperJumpOnRoofFireWindowFinder.TryGetRoofTarget(
                    planningState.Hamster,
                    decisionPoint.Chain,
                    out ObstacleSnapshot roofObstacle,
                    out int roofWorldIndex,
                    out _)
                || roofObstacle.InstanceId != targetObstacle.InstanceId)
            {
                return false;
            }

            // Восстанавливает текущий fire shift относительно trigger obstacle.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    out float fireShift))
            {
                return false;
            }

            // Пересчитывает допустимое roof landing окно для текущего мира.
            if (!SuperJumpOnRoofFireWindowFinder.TryGetRoofLandingWindow(
                    planningState,
                    roofObstacle,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift))
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
            return SuperJumpOnRoofFireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                action.PostFireWorldShift,
                roofWorldIndex);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift для retained action по trigger obstacle.
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