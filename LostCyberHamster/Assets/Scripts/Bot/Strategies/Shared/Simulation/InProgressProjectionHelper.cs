using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.Simulation
{
    /// <summary>
    /// Содержит общий расчёт projection для уже запущенного head-action.
    /// </summary>
    internal static class InProgressProjectionHelper
    {
        public static PlanningState Project(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            HamsterSnapshot nextHamster,
            bool skipTargetObstacleAfterCompletion,
            float? remainingPostFireWorldShift = null,
            int? startObstacleIndexOverride = null,
            int? removedObstacleInstanceIdAfterCompletion = null)
        {
            if (planningState == null || action == null || worldSnapshot == null || nextHamster == null)
                return null;

            float remainingPostFireShift = remainingPostFireWorldShift.HasValue
                ? remainingPostFireWorldShift.Value
                : GetRemainingPostFireShift(action, worldSnapshot);
            if (remainingPostFireShift < 0f)
                remainingPostFireShift = 0f;

            float nextProjectionWorldShift = planningState.ProjectionWorldShift + remainingPostFireShift;
            IReadOnlyList<int> nextRemovedObstacleInstanceIds =
                planningState.GetRemovedObstacleInstanceIdsWith(removedObstacleInstanceIdAfterCompletion);

            int startObstacleIndex = startObstacleIndexOverride ?? planningState.NextObstacleIndex;
            if (removedObstacleInstanceIdAfterCompletion.HasValue)
            {
                startObstacleIndex = startObstacleIndexOverride ?? 0;
            }
            else if (skipTargetObstacleAfterCompletion && action.TargetObstacleIndex + 1 > startObstacleIndex)
            {
                startObstacleIndex = action.TargetObstacleIndex + 1;
            }

            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX,
                nextRemovedObstacleInstanceIds);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift,
                nextRemovedObstacleInstanceIds);
        }

        private static float GetRemainingPostFireShift(PlannedAction action, WorldSnapshot worldSnapshot)
        {
            float remainingPostFireShift = action.PostFireWorldShift;
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (!triggerObstacleInstanceId.HasValue)
                return remainingPostFireShift;

            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                    continue;

                float shiftSinceFire = action.TriggerX - obstacle.LeftX;
                if (shiftSinceFire > 0f)
                    remainingPostFireShift = action.PostFireWorldShift - shiftSinceFire;

                break;
            }

            return remainingPostFireShift < 0f ? 0f : remainingPostFireShift;
        }

        private static int FindNextRelevantObstacleIndex(
            WorldSnapshot worldSnapshot,
            int startObstacleIndex,
            float projectionWorldShift,
            float hamsterLeftX,
            IReadOnlyList<int> removedObstacleInstanceIds)
        {
            for (int obstacleIndex = startObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (IsObstacleRemoved(obstacle.InstanceId, removedObstacleInstanceIds))
                    continue;

                float projectedRightX = obstacle.RightX - projectionWorldShift;
                if (projectedRightX > hamsterLeftX)
                    return obstacleIndex;
            }

            return worldSnapshot.Obstacles.Count;
        }

        private static bool IsObstacleRemoved(
            int obstacleInstanceId,
            IReadOnlyList<int> removedObstacleInstanceIds)
        {
            if (removedObstacleInstanceIds == null)
                return false;

            for (int index = 0; index < removedObstacleInstanceIds.Count; index++)
            {
                if (removedObstacleInstanceIds[index] == obstacleInstanceId)
                    return true;
            }

            return false;
        }
    }
}
