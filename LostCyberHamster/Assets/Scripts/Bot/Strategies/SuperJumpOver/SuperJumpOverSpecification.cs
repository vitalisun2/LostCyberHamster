using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Проверяет применимость super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverSpecification
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

            if (!decisionPoint.IsBlockingThreat())
                return false;

            if (!ObstacleClassifier.CanSuperJumpOverOnGround(decisionPoint.Obstacle.ObstacleType))
                return false;

            targetObstacle = decisionPoint.Obstacle;
            targetObstacleIndex = decisionPoint.ObstacleIndex;
            return true;
        }
    }
}
