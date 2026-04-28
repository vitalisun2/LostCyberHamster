using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Ищет fire shift для super jump landing на крышу.
    /// </summary>
    internal sealed class SuperJumpOnRoofFireWindowCalculator
    {
        private readonly JumpOutcomeFireWindowCalculator _calculator;

        public SuperJumpOnRoofFireWindowCalculator()
        {
            _calculator = new JumpOutcomeFireWindowCalculator(
                new RoofLandingSearchWindowPolicy(),
                HamsterStateEnum.SuperJumpOnRoof,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump,
                new GroundContactPreFireSafetyPolicy(),
                diagnosticPrefix: "SuperJumpOnRoof");
        }

        public JumpOutcomeFireWindowCalculator OutcomeCalculator => _calculator;

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            bool preferLatestFireShift,
            out float fireShift)
        {
            return _calculator.TryFindFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                superJumpTravel,
                preferLatestFireShift,
                out fireShift);
        }
    }
}