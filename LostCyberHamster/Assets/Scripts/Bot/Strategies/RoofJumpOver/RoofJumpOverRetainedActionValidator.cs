using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный roof-jump-over action.
    /// </summary>
    internal sealed class RoofJumpOverRetainedActionValidator : IRetainedActionValidator
    {
        public BotActionKind ActionKind => BotActionKind.RoofJumpOver;

        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            DecisionPoint decisionPoint = context.DecisionPoint;
            ObstacleSnapshot targetObstacle = context.TargetObstacle;
            PlannedAction action = context.Action;

            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || targetObstacle == null)
            {
                return false;
            }

            if (targetObstacle.ObstacleType != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                return false;

            if (decisionPoint.Chain.FirstObstacle.InstanceId != targetObstacle.InstanceId)
                return false;

            if (!action.ResultRoofSupportInstanceId.HasValue)
                return false;

            if (!RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    out ObstacleSnapshot supportObstacle,
                    out _))
            {
                return false;
            }

            if (supportObstacle.InstanceId != action.ResultRoofSupportInstanceId.Value)
                return false;

            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            if (!RoofJumpOverFireWindowFinder.TryGetOpenWindow(
                    planningState.Hamster,
                    targetObstacle,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            if (fireShift < firstFireShift || fireShift > lastFireShift)
                return false;

            if (!RoofJumpOverStrategy.TryGetRoofJumpOverTravel(
                    out float roofJumpOverTravel,
                    out float jumpFromRoofTravel))
            {
                return false;
            }

            return RoofJumpOverFireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                JumpObstacleProjection.BuildBase(projectedWorldSnapshot),
                targetObstacle.InstanceId,
                fireShift,
                roofJumpOverTravel,
                jumpFromRoofTravel);
        }

        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            float projectedTriggerX = action.TriggerX - projectionWorldShift;
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - projectedTriggerX;
                    return true;
                }
            }

            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
