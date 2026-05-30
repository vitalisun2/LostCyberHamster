using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver
{
    /// <summary>
    /// Подбирает fire shift для ground jump-over chain.
    /// </summary>
    internal sealed class JumpOverFireWindowFinder
    {
        private readonly IJumpOverPolicy _policy;

        public JumpOverFireWindowFinder(IJumpOverPolicy policy)
        {
            _policy = policy;
        }

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            float jumpTravel,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            if (!JumpOverChainCalculator.TryCalculate(
                    _policy,
                    planningState.Hamster,
                    chain,
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

        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            JumpOverChainModel chainWindow)
        {
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

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
