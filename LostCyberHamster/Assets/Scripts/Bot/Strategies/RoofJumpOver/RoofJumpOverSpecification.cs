using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Проверяет применимость roof jump over над small obstacle на крыше.
    /// </summary>
    internal sealed class RoofJumpOverSpecification
    {
        public const int EnergyCost = 10;

        public bool IsSatisfiedBy(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            WorldSnapshot projectedWorldSnapshot,
            out ObstacleSnapshot hazardObstacle,
            out int hazardObstacleIndex,
            out ObstacleSnapshot supportObstacle,
            out int supportObstacleIndex)
        {
            hazardObstacle = null;
            hazardObstacleIndex = -1;
            supportObstacle = null;
            supportObstacleIndex = -1;

            if (planningState == null
                || decisionPoint?.Chain == null
                || projectedWorldSnapshot == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.HamsterState != HamsterStateEnum.RoofRun
                || !hamster.IsOnRoof
                || !hamster.RoofSupportInstanceId.HasValue
                || hamster.IsShifting
                || hamster.IsDamaged
                || hamster.Energy < EnergyCost)
            {
                return false;
            }

            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (firstObstacle.ObstacleType != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                return false;

            if (firstObstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (!RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                    planningState,
                    projectedWorldSnapshot,
                    firstObstacle,
                    out supportObstacle,
                    out supportObstacleIndex))
            {
                return false;
            }

            hazardObstacle = firstObstacle;
            hazardObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }
    }
}
