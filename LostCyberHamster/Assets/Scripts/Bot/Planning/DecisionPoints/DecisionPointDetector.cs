using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Находит следующую обязательную для реакции ситуацию в projected world snapshot.
    /// </summary>
    public sealed class DecisionPointDetector
    {
        /// <summary>
        /// Ограничивает количество obstacles в одной chain-ситуации.
        /// </summary>
        private const int _maxChainLength = 3;

        /// <summary>
        /// Пытается найти ближайшую chain-ситуацию на текущей линии хомяка.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;

            // Отсекает неполный вход.
            if (planningState == null || worldSnapshot == null)
                return false;

            // Выбирает стартовый obstacle.
            int firstObstacleIndex = GetFirstDetectionIndex(planningState, worldSnapshot);

            // Ищет ближайший damaging obstacle на текущей линии.
            for (int obstacleIndex = firstObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                    continue;

                if (RoofRunProjection.IsPassiveRoofContinuation(planningState, worldSnapshot, obstacle))
                {
                    DebugManager.DiagLog(
                        $"[Bot PLAN] SKIP_ROOF_CONTINUATION obstacle={obstacle.ObstacleType} " +
                        $"index={obstacleIndex} instanceId={obstacle.InstanceId} " +
                        $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2}");
                    continue;
                }

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                // Создает decision point.
                decisionPoint = new DecisionPoint(BuildChain(planningState, worldSnapshot, obstacleIndex));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает index obstacle, с которого detector должен начать поиск decision point.
        /// </summary>
        private static int GetFirstDetectionIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            // Готовит default start.
            int defaultDetectionIndex = planningState.NextObstacleIndex;
            HamsterSnapshot hamster = planningState.Hamster;

            // Разделяет ground и roof-сценарии.
            if (hamster == null || !hamster.IsOnRoof)
                return defaultDetectionIndex;

            // Пробует пропустить passive roof chain.
            if (RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out int lastRoofIndex))
            {
                int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
                if (firstIndexAfterPassiveRoofs > defaultDetectionIndex)
                {
                    DebugManager.DiagLog(
                        $"[Bot PLAN] SKIP_PASSIVE_ROOF_CHAIN lastRoof={lastRoof.ObstacleType} " +
                        $"index={lastRoofIndex} instanceId={lastRoof.InstanceId} " +
                        $"leftX={lastRoof.LeftX:F2} rightX={lastRoof.RightX:F2}");

                    return firstIndexAfterPassiveRoofs;
                }
            }

            // Возвращает default fallback.
            return defaultDetectionIndex;
        }

        /// <summary>
        /// Строит obstacle chain, начиная с первого obstacle decision point.
        /// </summary>
        private static ObstacleChain BuildChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex)
        {
            // Инициализирует chain первым obstacle.
            var obstacles = new List<ObstacleSnapshot>();
            var indices = new List<int>();
            ObstacleSnapshot firstObstacle = worldSnapshot.Obstacles[firstObstacleIndex];
            obstacles.Add(firstObstacle);
            indices.Add(firstObstacleIndex);

            // Расширяет chain близкими damaging obstacles.
            float previousRightX = firstObstacle.RightX;
            for (int obstacleIndex = firstObstacleIndex + 1;
                 obstacleIndex < worldSnapshot.Obstacles.Count && obstacles.Count < _maxChainLength;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
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
            return new ObstacleChain(obstacles, indices);
        }
    }
}
