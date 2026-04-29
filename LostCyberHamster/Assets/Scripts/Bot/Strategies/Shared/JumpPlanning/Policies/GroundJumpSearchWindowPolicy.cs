using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies
{
    /// <summary>
    /// Рассчитывает окно fire shift для ground jump-over действий.
    /// </summary>
    internal sealed class GroundJumpSearchWindowPolicy : IJumpSearchWindowPolicy
    {
        public bool TryGetSearchWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            float chainRightX = GetRoadSmallChainRightX(projectedWorldSnapshot, targetObstacle, targetObstacleIndex);
            firstFireShift = chainRightX - hamster.HamsterLeftX - actionTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = targetObstacle.LeftX - hamster.HamsterRightX;
            return lastFireShift >= 0f && firstFireShift <= lastFireShift;
        }

        private static float GetRoadSmallChainRightX(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex)
        {
            float chainRightX = targetObstacle.RightX;
            if (projectedWorldSnapshot == null
                || targetObstacleIndex < 0
                || targetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count
                || !ObstacleClassifier.IsRoadSmallOverChainObstacle(targetObstacle.ObstacleType))
            {
                return chainRightX;
            }

            bool isBottomLine = targetObstacle.IsBottomLine;
            for (int obstacleIndex = targetObstacleIndex + 1; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(obstacle.ObstacleType))
                    break;

                chainRightX = obstacle.RightX;
            }

            return chainRightX;
        }
    }
}
