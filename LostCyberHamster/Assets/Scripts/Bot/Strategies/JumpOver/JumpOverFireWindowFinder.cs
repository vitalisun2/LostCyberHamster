using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.JumpOver.Models;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Ищет fire shift для обычного jump-over.
    /// </summary>
    internal sealed class JumpOverFireWindowFinder
    {
        /// <summary>
        /// Подбирает fire shift внутри допустимого окна для jump-over.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)));

            if (!JumpOverChainCalculator.TryCalculate(
                    planningState.Hamster,
                    projectedWorldSnapshot,
                    targetObstacleIndex,
                    jumpTravel,
                    out JumpOverChainModel chainWindow))
            {
                fireShift = 0f;
                return false;
            }

            fireShift = chainWindow.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                jumpTravel,
                chainWindow);
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
