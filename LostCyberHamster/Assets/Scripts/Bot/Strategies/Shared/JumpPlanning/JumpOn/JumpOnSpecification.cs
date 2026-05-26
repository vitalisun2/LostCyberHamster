using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Проверяет применимость ground jump-on действия.
    /// </summary>
    internal sealed class JumpOnSpecification
    {
        private readonly IJumpOnPolicy _policy;

        public JumpOnSpecification(IJumpOnPolicy policy)
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
            if (hamster == null
                || hamster.IsOnRoof
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (firstObstacle.IsBottomLine != hamster.IsOnBottomLine
                || !ObstacleClassifier.CanJumpOnGroundObstacle(firstObstacle.ObstacleType))
            {
                return false;
            }

            targetObstacle = firstObstacle;
            targetObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }
    }
}
