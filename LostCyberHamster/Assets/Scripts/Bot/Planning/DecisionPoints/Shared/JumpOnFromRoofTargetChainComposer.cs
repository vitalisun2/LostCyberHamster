using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Собирает дорожную target-chain после passive roof path.
    /// </summary>
    internal static class JumpOnFromRoofTargetChainComposer
    {
        /// <summary>
        /// Возвращает road-chain после конца passive roof path, если она ведет к roof jump-on target.
        /// </summary>
        public static bool TryBuildTargetChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            float maxTargetLeftX,
            out ObstacleChain targetChain,
            out ObstacleSnapshot lastRoof)
        {
            // Проверяет обязательный контекст.
            targetChain = null;
            lastRoof = null;
            if (planningState == null || planningState.Hamster == null || worldSnapshot == null)
                return false;

            // Находит конец текущей passive roof path.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out lastRoof,
                    out int lastRoofIndex))
            {
                return false;
            }

            // Собирает road-chain после правого края последней крыши до первого target.
            var obstacles = new List<ObstacleSnapshot>();
            var indices = new List<int>();
            for (int obstacleIndex = lastRoofIndex + 1;
                 obstacleIndex < worldSnapshot.Obstacles.Count;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.LeftX > maxTargetLeftX)
                    break;

                if (!IsRoadChainObstacle(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        lastRoof.RightX))
                {
                    continue;
                }

                obstacles.Add(obstacle);
                indices.Add(obstacleIndex);

                if (!ObstacleClassifier.CanJumpOnFromRoofObstacle(obstacle.ObstacleType))
                    continue;

                targetChain = new ObstacleChain(obstacles, indices);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, должен ли obstacle участвовать в road-chain.
        /// </summary>
        private static bool IsRoadChainObstacle(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            float roofRightEdgeX)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            if (obstacle == null)
                return false;

            if (obstacle.RightX <= hamster.HamsterLeftX)
                return false;

            if (obstacle.LeftX < roofRightEdgeX)
                return false;

            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                return false;

            if (RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                    planningState,
                    worldSnapshot,
                    obstacle,
                    out _,
                    out _))
            {
                return false;
            }

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }
    }
}
