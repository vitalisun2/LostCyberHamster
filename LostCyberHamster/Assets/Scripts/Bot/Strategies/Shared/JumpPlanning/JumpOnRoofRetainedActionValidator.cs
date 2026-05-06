using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранное действие запрыгивания на крышу.
    /// </summary>
    internal sealed class JumpOnRoofRetainedActionValidator : IRetainedActionValidator
    {
        private readonly IJumpOnRoofPolicy _policy;
        private readonly JumpOnRoofFireWindowFinder _fireWindowFinder;

        public JumpOnRoofRetainedActionValidator(
            IJumpOnRoofPolicy policy,
            JumpOnRoofFireWindowFinder fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        /// <summary>
        /// Тип действия, которое умеет сохранять validator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, что сохранённое действие запрыгивания на крышу всё ещё соответствует текущему planning context.
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

            // Проверяет, что текущая chain всё ещё ведёт к той же крыше.
            if (!decisionPoint.Chain.TryFindFirstRoof(
                    out ObstacleSnapshot roofObstacle,
                    out int roofWorldIndex,
                    out _)
                || roofObstacle.InstanceId != targetObstacle.InstanceId)
            {
                return false;
            }

            // Считает, сколько осталось до сохранённого TriggerX.
            if (!TryGetRemainingShiftToTrigger(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    out float remainingShiftToTrigger))
            {
                return false;
            }

            // Если trigger уже пройден, retained action больше нельзя удерживать.
            if (remainingShiftToTrigger < 0f)
                return false;

            // Сверяет runtime outcome с ожидаемой посадкой на крышу.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                remainingShiftToTrigger,
                action.PostFireWorldShift,
                roofWorldIndex);
        }

        /// <summary>
        /// Считает оставшееся расстояние от trigger obstacle до сохранённой точки запуска action.
        /// </summary>
        private static bool TryGetRemainingShiftToTrigger(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            out float remainingShiftToTrigger)
        {
            // Проверяет наличие исходных данных.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                remainingShiftToTrigger = 0f;
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

                    remainingShiftToTrigger = obstacle.LeftX - action.TriggerX;
                    return true;
                }
            }

            // Использует target obstacle как fallback.
            remainingShiftToTrigger = targetObstacle.LeftX - action.TriggerX;
            return true;
        }
    }
}
