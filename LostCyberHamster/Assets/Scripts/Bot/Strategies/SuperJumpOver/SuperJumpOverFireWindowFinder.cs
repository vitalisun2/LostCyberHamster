using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.SuperJumpOver.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Ищет fire moment для super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverFireWindowFinder
    {
        /// <summary>
        /// Подбирает момент fire внутри допустимого окна для super jump-over.
        /// </summary>
        public bool TryFindFireMoment(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireMoment)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)));

            if (!SuperJumpOverChainCalculator.TryCalculate(
                    planningState.Hamster,
                    projectedWorldSnapshot,
                    targetObstacleIndex,
                    superJumpTravel,
                    out SuperJumpOverChainModel chainWindow))
            {
                fireMoment = 0f;
                return false;
            }

            fireMoment = chainWindow.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireMoment(
                planningState.Hamster,
                baseObstacles,
                fireMoment,
                superJumpTravel,
                chainWindow);
        }

        /// <summary>
        /// Проверяет, что fire moment приводит к ожидаемому runtime outcome по рассчитанной chain.
        /// </summary>
        private static bool CheckRuntimeOutcomeAtFireMoment(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireMoment,
            float superJumpTravel,
            SuperJumpOverChainModel chainWindow)
        {
            // Строит obstacle snapshot на момент fire.
            var obstaclesAtFireMoment = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireMoment, obstaclesAtFireMoment);

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
            JumpResolveResult result = SuperJumpOutcomeResolver.ResolveSuperJump(obstaclesAtFireMoment, context);
            return result.State == HamsterStateEnum.SuperJumpOver
                   && chainWindow.ContainsObstacleIndex(result.TargetIndex);
        }
    }
}
