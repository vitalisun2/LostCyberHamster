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
                || decisionPoint.Chain == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsDamaged || hamster.IsShifting)
                return false;

            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (!ObstacleClassifier.DamagesOnGroundContact(firstObstacle.ObstacleType)
                || IsClearDirectRoofLanding(decisionPoint.Chain))
            {
                return false;
            }

            targetObstacle = firstObstacle;
            targetObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }

        public bool IsSatisfiedBy(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            if (planningState == null || targetObstacle == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            return !hamster.IsOnRoof && !hamster.IsDamaged && !hamster.IsShifting;
        }

        /// <summary>
        /// Возвращает true, если первый obstacle chain является чистой крышей для прямой посадки.
        /// </summary>
        private static bool IsClearDirectRoofLanding(ObstacleChain chain)
        {
            ObstacleSnapshot firstObstacle = chain.FirstObstacle;
            return ObstacleClassifier.IsObstacleWithRoof(firstObstacle.ObstacleType)
                   && !chain.HasDamagingRoofOccupant(0);
        }
    }
}
