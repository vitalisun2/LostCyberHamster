using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.GameEngine.Mechanics;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Собирает общую damaging-obstacle chain для builders, работающих от road threat.
    /// </summary>
    internal static class ThreatChainCollector
    {
        /// <summary>
        /// Ограничивает количество obstacles в одной threat-chain.
        /// </summary>
        private const int MaxChainLength = 3;

        /// <summary>
        /// Пытается построить chain от ближайшей угрозы текущей линии.
        /// </summary>
        public static bool TryBuildNearestThreatChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            out ObstacleChain chain)
        {
            // Находит первый obstacle, который требует обязательной реакции.
            chain = null;
            if (!TryFindFirstThreat(
                    planningState,
                    worldSnapshot,
                    firstObstacleIndex,
                    out int threatIndex))
            {
                return false;
            }

            // Строит chain от найденной угрозы.
            return TryBuildChainFromIndex(
                planningState,
                worldSnapshot,
                threatIndex,
                planningState.IsOnBottomLine,
                out chain);
        }

        /// <summary>
        /// Пытается построить damaging-chain от уже найденного obstacle.
        /// </summary>
        public static bool TryBuildChainFromIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            bool chainBottomLine,
            out ObstacleChain chain)
        {
            // Проверяет вход и индекс.
            chain = null;
            if (planningState?.Hamster == null
                || worldSnapshot?.Obstacles == null
                || firstObstacleIndex < 0
                || firstObstacleIndex >= worldSnapshot.Obstacles.Count)
            {
                return false;
            }

            // Инициализирует chain первым obstacle.
            var obstacles = new List<ObstacleSnapshot>();
            var indices = new List<int>();
            ObstacleSnapshot firstObstacle = worldSnapshot.Obstacles[firstObstacleIndex];
            obstacles.Add(firstObstacle);
            indices.Add(firstObstacleIndex);

            // Расширяет chain близкими damaging obstacles.
            float previousRightX = firstObstacle.RightX;
            for (int obstacleIndex = firstObstacleIndex + 1;
                 obstacleIndex < worldSnapshot.Obstacles.Count && obstacles.Count < MaxChainLength;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != chainBottomLine)
                    continue;

                if (RoofRunProjection.IsPassiveRoofContinuation(planningState, worldSnapshot, obstacle))
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                float gap = obstacle.LeftX - previousRightX;
                if (gap >= planningState.Hamster.Width)
                    break;

                obstacles.Add(obstacle);
                indices.Add(obstacleIndex);

                if (obstacle.RightX > previousRightX)
                    previousRightX = obstacle.RightX;
            }

            // Возвращает готовую chain.
            chain = new ObstacleChain(obstacles, indices);
            return true;
        }

        /// <summary>
        /// Находит ближайшую обязательную угрозу на текущей линии хомяка.
        /// </summary>
        public static bool TryFindFirstThreat(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            out int threatIndex)
        {
            // Подготавливает результат.
            threatIndex = -1;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            // Сканирует obstacles до первой угрозы текущей линии.
            for (int obstacleIndex = firstObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!IsThreat(planningState, worldSnapshot, obstacle, obstacleIndex))
                    continue;

                threatIndex = obstacleIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, является ли obstacle обязательной угрозой текущей линии.
        /// </summary>
        private static bool IsThreat(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            int obstacleIndex)
        {
            if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                return false;

            if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                return false;

            if (RoofRunProjection.IsPassiveRoofContinuation(planningState, worldSnapshot, obstacle))
            {
                DebugManager.DiagLogVerbose(
                    $"[Bot PLAN] SKIP_ROOF_CONTINUATION obstacle={obstacle.ObstacleType} " +
                    $"index={obstacleIndex} instanceId={obstacle.InstanceId} " +
                    $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2}");
                return false;
            }

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }
    }
}
