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

            if (planningState == null || worldSnapshot == null)
                return false;

            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
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

                decisionPoint = new DecisionPoint(BuildChain(planningState, worldSnapshot, obstacleIndex));
                return true;
            }

            return false;
        }

        private static ObstacleChain BuildChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex)
        {
            var obstacles = new List<ObstacleSnapshot>();
            var indices = new List<int>();
            ObstacleSnapshot firstObstacle = worldSnapshot.Obstacles[firstObstacleIndex];
            obstacles.Add(firstObstacle);
            indices.Add(firstObstacleIndex);

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

            return new ObstacleChain(obstacles, indices);
        }
    }
}