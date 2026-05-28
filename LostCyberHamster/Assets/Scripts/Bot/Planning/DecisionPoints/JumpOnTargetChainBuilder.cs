using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит target-oriented chain для ground jump-on, не меняя общий blocking-chain detector.
    /// </summary>
    internal static class JumpOnTargetChainBuilder
    {
        /// <summary>
        /// Возвращает исходную или расширенную chain, если она ведет к первому ground jump-on target на линии chain.
        /// </summary>
        public static bool TryBuildTargetChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleChain sourceChain,
            float maxTargetLeftX,
            out ObstacleChain targetChain)
        {
            targetChain = null;

            // Проверяет обязательный контекст.
            if (planningState == null
                || planningState.Hamster == null
                || worldSnapshot == null
                || sourceChain == null
                || sourceChain.Count <= 0)
            {
                return false;
            }

            // Использует готовую chain, если target уже находится внутри нее.
            bool chainBottomLine = sourceChain.FirstObstacle.IsBottomLine;
            if (sourceChain.TryFindFirstGroundJumpOnTarget(
                    chainBottomLine,
                    out ObstacleSnapshot sourceTarget,
                    out _,
                    out _))
            {
                if (sourceTarget.LeftX > maxTargetLeftX)
                    return false;

                targetChain = sourceChain;
                return true;
            }

            // Расширяет chain до первого target, оставляя все pre-target obstacles частью одного действия.
            var obstacles = new List<ObstacleSnapshot>(sourceChain.Obstacles);
            var indices = new List<int>(sourceChain.Indices);
            int scanStartIndex = sourceChain.Indices[sourceChain.Count - 1] + 1;

            for (int obstacleIndex = scanStartIndex;
                 obstacleIndex < worldSnapshot.Obstacles.Count;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanIncludeInJumpOnTargetChain(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        chainBottomLine))
                {
                    continue;
                }

                if (obstacle.LeftX > maxTargetLeftX)
                    return false;

                obstacles.Add(obstacle);
                indices.Add(obstacleIndex);

                if (!ObstacleClassifier.CanJumpOnGroundObstacle(obstacle.ObstacleType))
                    continue;

                targetChain = new ObstacleChain(obstacles, indices);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, должен ли obstacle участвовать в target-aware jump-on chain.
        /// </summary>
        private static bool CanIncludeInJumpOnTargetChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            bool chainBottomLine)
        {
            if (obstacle == null)
                return false;

            if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                return false;

            if (obstacle.IsBottomLine != chainBottomLine)
                return false;

            if (RoofRunProjection.IsPassiveRoofContinuation(planningState, worldSnapshot, obstacle))
                return false;

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }
    }
}
