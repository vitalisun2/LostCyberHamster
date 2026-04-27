using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Проверяет применимость обычного jump-over.
    /// </summary>
    internal sealed class JumpOverSpecification
    {
        public const int EnergyCost = 10;

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
                || decisionPoint.Kind != DecisionPointKind.BlockingObstacle
                || decisionPoint.Obstacle == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.Energy < EnergyCost)
                return false;

            if (!ObstacleClassifier.CanJumpOverOnGround(decisionPoint.Obstacle.ObstacleType))
                return false;

            targetObstacle = decisionPoint.Obstacle;
            targetObstacleIndex = decisionPoint.ObstacleIndex;
            return true;
        }
    }
}
