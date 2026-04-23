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
        /// Возвращает тип действия, который стратегия будет использовать для roof landing.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.Jump;

        /// <summary>
        /// Собирает кандидаты обычного прыжка, которые должны завершиться посадкой хомяка на крышу obstacle.
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
        /// Симулирует planning-состояние после успешного landing на крышу и перехода в RoofRun.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Проверяет, допускает ли текущее planning-состояние обычный прыжок с посадкой на крышу target obstacle.
        /// </summary>
        private static bool CanJumpToRoof(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Возвращает длину runtime jump-клипа, чтобы стратегия искала fire window в том же тайминге, что и gameplay.
        /// </summary>
        private bool TryGetJumpTravel(out float jumpTravel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Создаёт planned action для roof landing после того, как будет найден корректный fire shift.
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