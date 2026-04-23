using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    /// <summary>
    /// Готовит planning-каркас для обычного прыжка с посадкой на препятствие с крышей.
    /// </summary>
    public sealed class JumpToRoofStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Тип действия для roof landing.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.Jump;

        /// <summary>
        /// Собирает кандидаты прыжка на крышу.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Симулирует переход в RoofRun.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Проверяет допустимость прыжка на крышу.
        /// </summary>
        private static bool CanJumpToRoof(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Возвращает длину jump-клипа.
        /// </summary>
        private bool TryGetJumpTravel(out float jumpTravel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Строит action для прыжка на крышу.
        /// </summary>
        private static PlannedAction BuildJumpToRoofAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float jumpTravel)
        {
            throw new NotImplementedException();
        }
    }
}