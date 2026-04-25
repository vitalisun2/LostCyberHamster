using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Описывает roof-obstacles, которые runtime пройдет как продолжение RoofRun без нового действия.
    /// </summary>
    internal static class RoofRunProjection
    {
        private const float PassiveContinuationGapFactor = 0.7f;

        public static bool IsPassiveRoofContinuation(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot obstacle)
        {
            if (planningState == null || projectedWorldSnapshot == null || obstacle == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.IsOnRoof || !hamster.RoofSupportInstanceId.HasValue)
                return false;

            if (!IsSameLaneRoof(hamster, obstacle))
                return false;

            int roofSupportInstanceId = hamster.RoofSupportInstanceId.Value;
            if (obstacle.InstanceId == roofSupportInstanceId)
                return true;

            ObstacleSnapshot currentSupport = FindRoofSupport(
                projectedWorldSnapshot,
                hamster,
                roofSupportInstanceId);

            if (currentSupport == null)
                return OverlapsHamster(hamster, obstacle);

            if (obstacle.RightX <= currentSupport.LeftX)
                return OverlapsHamster(hamster, obstacle);

            float gap = obstacle.LeftX - currentSupport.RightX;
            if (gap <= 0f)
                return true;

            float maxPassiveGap = hamster.Width * PassiveContinuationGapFactor;
            return gap <= maxPassiveGap;
        }

        private static ObstacleSnapshot FindRoofSupport(
            WorldSnapshot projectedWorldSnapshot,
            HamsterSnapshot hamster,
            int roofSupportInstanceId)
        {
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot candidate = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (candidate.InstanceId == roofSupportInstanceId && IsSameLaneRoof(hamster, candidate))
                    return candidate;
            }

            return null;
        }

        private static bool IsSameLaneRoof(HamsterSnapshot hamster, ObstacleSnapshot obstacle)
        {
            return obstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
        }

        private static bool OverlapsHamster(HamsterSnapshot hamster, ObstacleSnapshot obstacle)
        {
            return obstacle.RightX > hamster.HamsterLeftX
                && obstacle.LeftX < hamster.HamsterRightX;
        }
    }
}
