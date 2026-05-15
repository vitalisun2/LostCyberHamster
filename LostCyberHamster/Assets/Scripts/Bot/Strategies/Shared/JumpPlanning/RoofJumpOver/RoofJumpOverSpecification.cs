using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Проверяет применимость roof jump-over действия.
    /// </summary>
    internal sealed class RoofJumpOverSpecification
    {
        private readonly IRoofJumpOverPolicy _policy;

        public RoofJumpOverSpecification(IRoofJumpOverPolicy policy)
        {
            _policy = policy;
        }

        public bool IsSatisfiedBy(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot hazardObstacle,
            out int hazardObstacleIndex)
        {
            hazardObstacle = null;
            hazardObstacleIndex = -1;

            if (planningState == null || projectedWorldSnapshot == null || decisionPoint?.Chain == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null
                || hamster.HamsterState != HamsterStateEnum.RoofRun
                || !hamster.IsOnRoof
                || !hamster.RoofSupportInstanceId.HasValue
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (!RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                    planningState,
                    projectedWorldSnapshot,
                    firstObstacle,
                    out _,
                    out _))
            {
                return false;
            }

            hazardObstacle = firstObstacle;
            hazardObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }
    }
}