using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Проверяет применимость обычного jump-over.
    /// </summary>
    internal sealed class JumpOverSpecification
    {
        /// <summary>
        /// Energy cost обычного jump-over.
        /// </summary>
        public const int EnergyCost = 10;

        /// <summary>
        /// Проверяет, что decision point можно закрыть обычным jump-over.
        /// </summary>
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
                || decisionPoint.Chain == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.Energy < EnergyCost)
                return false;

            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (!ObstacleClassifier.CanJumpOverOnGround(firstObstacle.ObstacleType))
                return false;

            targetObstacle = firstObstacle;
            targetObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }
    }
}
