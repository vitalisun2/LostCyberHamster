using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOver
{
    /// <summary>
    /// Подбирает fire shift для role-based ground jump-over chain.
    /// </summary>
    internal sealed class JumpOverFireWindowFinder
    {
        private readonly IJumpOverPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного jump-over policy.
        /// </summary>
        public JumpOverFireWindowFinder(IJumpOverPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift и подтвердить его runtime-equivalent resolver'ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            float jumpTravel,
            out JumpOverChainModel chainWindow,
            out float fireShift)
        {
            // Проверяет входы и вычисляет геометрическое окно.
            chainWindow = default;
            fireShift = 0f;
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            if (!JumpOverChainCalculator.TryCalculate(
                    _policy,
                    planningState.Hamster,
                    chain,
                    jumpTravel,
                    out chainWindow))
            {
                return false;
            }

            // Проверяет выбранный fire shift через runtime resolver.
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
        /// Проверяет, что runtime resolver завершит прыжок ожидаемым over-состоянием.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            JumpOverChainModel chainWindow)
        {
            // Проецирует obstacle snapshot к моменту запуска.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Делегирует финальную проверку runtime-equivalent resolver'у policy.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                jumpTravel,
                jumpTravel,
                damageBigAliveWithoutYByReach: _policy.DamageBigAliveWithoutYByReach);

            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            return result.State == _policy.ExpectedOverState
                   && chainWindow.IsLastObstacle(result.TargetIndex);
        }
    }
}
