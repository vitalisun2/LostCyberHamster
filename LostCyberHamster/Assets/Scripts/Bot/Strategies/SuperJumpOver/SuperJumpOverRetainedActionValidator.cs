using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.SuperJumpOver.Models;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный super jump-over action.
    /// </summary>
    internal sealed class SuperJumpOverRetainedActionValidator : IRetainedActionValidator
    {
        private const float _validationEpsilon = 0.0001f;

        /// <summary>
        /// Тип действия, которое умеет сохранять validator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpOver;

        /// <summary>
        /// Проверяет, что сохранённое действие super jump-over всё ещё соответствует текущему planning context.
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

            if (decisionPoint.Chain.FirstObstacle.InstanceId != targetObstacle.InstanceId)
                return false;

            // Пересчитывает допустимое окно chain для текущего мира.
            if (!SuperJumpOverChainCalculator.TryCalculate(
                    planningState.Hamster,
                    decisionPoint.Chain,
                    action.PostFireWorldShift,
                    out SuperJumpOverChainModel chainWindow))
            {
                return false;
            }

            // Восстанавливает текущий fire shift retained action.
                if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
                {
                return false;
                }

            // Проверяет, что fire shift остаётся внутри допустимого окна.
            if (fireShift < chainWindow.FirstFireShift - _validationEpsilon
                || fireShift > chainWindow.LastFireShift + _validationEpsilon)
            {
                return false;
            }

            // Сверяет runtime outcome с ожидаемой chain.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                action.PostFireWorldShift,
                chainWindow);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift для retained action в projected-координатах.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            // Проверяет наличие исходных данных.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Переводит live trigger action обратно в projected-координату.
            float projectedTriggerX = action.TriggerX - projectionWorldShift;

            // Пытается найти trigger obstacle по instance id.
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - projectedTriggerX;
                    return true;
                }
            }

            // Использует target obstacle как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }

        /// <summary>
        /// Проверяет, что fire shift приводит к ожидаемому runtime outcome по рассчитанной chain.
        /// </summary>
        private static bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float superJumpTravel,
            SuperJumpOverChainModel chainWindow)
        {
            // Строит obstacle snapshot на момент fire.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Готовит контекст для runtime resolver'а.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                superJumpTravel,
                superJumpTravel,
                damageBigAliveWithoutYByReach: false);

            // Сверяет runtime outcome с ожидаемой chain.
            JumpResolveResult result = SuperJumpOutcomeResolver.ResolveSuperJump(obstaclesAtFireShift, context);
            return result.State == HamsterStateEnum.SuperJumpOver
                   && chainWindow.ContainsObstacleIndex(result.TargetIndex);
        }
    }
}
