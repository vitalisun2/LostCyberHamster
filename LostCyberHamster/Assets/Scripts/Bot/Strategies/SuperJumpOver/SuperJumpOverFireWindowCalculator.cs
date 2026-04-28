using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Ищет fire shift для super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverFireWindowCalculator
    {
        private readonly JumpOutcomeFireWindowCalculator _calculator;

        public SuperJumpOverFireWindowCalculator()
        {
            _calculator = new JumpOutcomeFireWindowCalculator(
                new GroundJumpSearchWindowPolicy(),
                HamsterStateEnum.SuperJumpOver,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump);
        }

        public JumpOutcomeFireWindowCalculator OutcomeCalculator => _calculator;

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireShift)
        {
            return _calculator.TryFindFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                superJumpTravel,
                preferLatestFireShift: false,
                out fireShift);
        }
    }
}
