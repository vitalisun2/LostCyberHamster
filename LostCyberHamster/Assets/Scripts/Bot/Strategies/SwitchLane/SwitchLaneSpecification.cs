using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Проверяет применимость смены линии для текущей planning-ситуации.
    /// </summary>
    internal sealed class SwitchLaneSpecification
    {
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
                || !decisionPoint.IsBlockingThreat()
                || decisionPoint.Obstacle == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsDamaged || hamster.IsShifting)
                return false;

            targetObstacle = decisionPoint.Obstacle;
            targetObstacleIndex = decisionPoint.ObstacleIndex;
            return true;
        }

        public bool IsSatisfiedBy(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            if (planningState == null || targetObstacle == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            return !hamster.IsOnRoof && !hamster.IsDamaged && !hamster.IsShifting;
        }
    }
}
