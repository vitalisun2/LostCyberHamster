using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Ищет fire shift для посадки на крышу.
    /// </summary>
    internal sealed class JumpOnRoofFireWindowCalculator
    {
        private readonly JumpOutcomeFireWindowCalculator _calculator;

        public JumpOnRoofFireWindowCalculator()
        {
            _calculator = new JumpOutcomeFireWindowCalculator(
                new RoofLandingSearchWindowPolicy(),
                HamsterStateEnum.JumpOnRoof,
                damageBigAliveWithoutYByReach: true,
                JumpOutcomeResolver.ResolveJump,
                diagnosticPrefix: "JumpOnRoof");
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
