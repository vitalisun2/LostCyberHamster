using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Ищет fire shift для обычного jump-over.
    /// </summary>
    internal sealed class JumpOverFireWindowCalculator
    {
        private readonly JumpOutcomeFireWindowCalculator _calculator;

        public JumpOverFireWindowCalculator()
        {
            _calculator = new JumpOutcomeFireWindowCalculator(
                new GroundJumpSearchWindowPolicy(),
                HamsterStateEnum.JumpOver,
                damageBigAliveWithoutYByReach: true,
                JumpOutcomeResolver.ResolveJump);
        }

        public JumpOutcomeFireWindowCalculator OutcomeCalculator => _calculator;

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float fireShift)
        {
            return _calculator.TryFindFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                jumpTravel,
                out fireShift);
        }
    }
}
