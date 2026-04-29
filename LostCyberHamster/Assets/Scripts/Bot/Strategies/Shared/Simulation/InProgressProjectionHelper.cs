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
            bool skipTargetObstacleAfterCompletion)
        {
            if (planningState == null || action == null || worldSnapshot == null || nextHamster == null)
                return null;

            float remainingPostFireShift = GetRemainingPostFireShift(action, worldSnapshot);
            float nextProjectionWorldShift = planningState.ProjectionWorldShift + remainingPostFireShift;
            int startObstacleIndex = planningState.NextObstacleIndex;
            if (skipTargetObstacleAfterCompletion && action.TargetObstacleIndex + 1 > startObstacleIndex)
                startObstacleIndex = action.TargetObstacleIndex + 1;

            int nextObstacleIndex = FindNextRelevantObstacleIndex(
                worldSnapshot,
                startObstacleIndex,
                nextProjectionWorldShift,
                nextHamster.HamsterLeftX);

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift);
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
            float hamsterLeftX)
        {
            for (int obstacleIndex = startObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - projectionWorldShift;
                if (projectedRightX > hamsterLeftX)
                    return obstacleIndex;
            }

            return worldSnapshot.Obstacles.Count;
        }
    }
}
