using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.JumpOver.Models;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный jump-over action.
    /// </summary>
    internal sealed class JumpOverRetainedActionValidator : IRetainedActionValidator
    {
        private const float _validationEpsilon = 0.0001f;

        /// <summary>
        /// Тип действия, которое умеет сохранять validator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOver;

        /// <summary>
        /// Проверяет, что сохранённое действие jump-over всё ещё соответствует текущему planning context.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Отсекает неподходящий retained action.
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            // Извлекает данные planning context.
            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            ObstacleSnapshot targetObstacle = context.TargetObstacle;
            int targetObstacleIndex = context.TargetObstacleIndex;
            PlannedAction action = context.Action;

            // Проверяет наличие обязательных данных.
            if (planningState == null || projectedWorldSnapshot == null || targetObstacle == null || action == null)
                return false;

            // Пересчитывает допустимое окно chain для текущего мира.
            if (!JumpOverChainCalculator.TryCalculate(
                    planningState.Hamster,
                    projectedWorldSnapshot,
                    targetObstacleIndex,
                    action.PostFireWorldShift,
                    out JumpOverChainModel chainWindow))
            {
                return false;
            }

            // Восстанавливает текущий fire shift retained action.
            if (!TryGetRemainingFireShift(projectedWorldSnapshot, targetObstacle, action, out float fireShift))
                return false;

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
        /// Восстанавливает оставшийся fire shift для retained action по live trigger obstacle.
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

            // Пытается найти live trigger obstacle по instance id.
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

        /// <summary>
        /// Проверяет, что fire shift приводит к ожидаемому runtime outcome по рассчитанной chain.
        /// </summary>
        private static bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            JumpOverChainModel chainWindow)
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
                jumpTravel,
                jumpTravel,
                damageBigAliveWithoutYByReach: true);

            // Сверяет runtime outcome с ожидаемой chain.
            JumpResolveResult result = JumpOutcomeResolver.ResolveJump(obstaclesAtFireShift, context);
            return result.State == HamsterStateEnum.JumpOver
                   && chainWindow.ContainsObstacleIndex(result.TargetIndex);
        }
    }
}
