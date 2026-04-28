using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Проверяет применимость super jump on roof.
    /// </summary>
    internal sealed class SuperJumpOnRoofSpecification
    {
        public const int EnergyCost = 20;

        public bool IsSatisfiedBy(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;

            if (planningState == null
                || decisionPoint == null
                || decisionPoint.Obstacle == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.IsDamaged || hamster.Energy < EnergyCost)
                return false;

            if (decisionPoint.Kind == DecisionPointKind.BlockingObstacleWithRoofLanding)
            {
                if (!ObstacleClassifier.CanSuperJumpOverOnGround(decisionPoint.Obstacle.ObstacleType)
                    || !decisionPoint.TryGetRoofLandingTarget(out targetObstacle, out targetObstacleIndex))
                {
                    return false;
                }

                return true;
            }

            if (decisionPoint.Kind != DecisionPointKind.RoofLanding)
                return false;

            return decisionPoint.TryGetRoofLandingTarget(out targetObstacle, out targetObstacleIndex);
        }
    }
}