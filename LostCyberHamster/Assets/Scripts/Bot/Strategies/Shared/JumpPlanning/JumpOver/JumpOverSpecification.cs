using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver
{
    /// <summary>
    /// Проверяет применимость ground jump-over действия.
    /// </summary>
    internal sealed class JumpOverSpecification
    {
        private readonly IJumpOverPolicy _policy;

        public JumpOverSpecification(IJumpOverPolicy policy)
        {
            _policy = policy;
        }

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
            if (hamster.IsOnRoof
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (firstObstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (!_policy.CanJumpOverObstacle(firstObstacle.ObstacleType))
                return false;

            targetObstacle = firstObstacle;
            targetObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }
    }
}
