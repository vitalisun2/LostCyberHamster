using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Находит следующую обязательную для реакции ситуацию в projected world snapshot.
    /// </summary>
    public sealed class DecisionPointDetector
    {
        /// <summary>
        /// Пытается найти ближайшую blocking-ситуацию на текущей линии хомяка.
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

                // Obstacles with roof выделяем в отдельную decision point, чтобы safe roof landing не смешивался с обычной blocking-угрозой.
                if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                {
                    if (TryFindRoofLandingHazard(worldSnapshot, obstacleIndex, out ObstacleSnapshot roofHazard))
                    {
                        DebugManager.DiagLog(
                            $"[Bot PLAN] OCCUPIED_ROOF obstacle={obstacle.ObstacleType} " +
                            $"index={obstacleIndex} instanceId={obstacle.InstanceId} " +
                            $"occupant={roofHazard.ObstacleType} occupantId={roofHazard.InstanceId} " +
                            $"leftX={obstacle.LeftX:F2} rightX={obstacle.RightX:F2}");

                        decisionPoint = BuildBlockingDecisionPoint(worldSnapshot, obstacle, obstacleIndex);
                        return true;
                    }

                    decisionPoint = new DecisionPoint(
                        DecisionPointKind.RoofLanding,
                        obstacle,
                        obstacleIndex);
                    return true;
                }

                decisionPoint = BuildBlockingDecisionPoint(worldSnapshot, obstacle, obstacleIndex);
                return true;
            }

            return false;
        }

        private static DecisionPoint BuildBlockingDecisionPoint(
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            int obstacleIndex)
        {
            if (TryFindRoofLandingContinuation(worldSnapshot, obstacle, obstacleIndex, out ObstacleSnapshot roofObstacle, out int roofObstacleIndex))
            {
                return new DecisionPoint(
                    DecisionPointKind.BlockingObstacleWithRoofLanding,
                    obstacle,
                    obstacleIndex,
                    roofObstacle,
                    roofObstacleIndex);
            }

            return new DecisionPoint(
                DecisionPointKind.BlockingObstacle,
                obstacle,
                obstacleIndex);
        }

        private static bool TryFindRoofLandingHazard(
            WorldSnapshot worldSnapshot,
            int roofObstacleIndex,
            out ObstacleSnapshot roofHazard)
        {
            var obstacleData = BuildObstacleData(worldSnapshot);
            if (JumpOutcomeResolver.TryFindDamagingRoofOccupantOnRoof(obstacleData, roofObstacleIndex, out int occupantIndex)
                && occupantIndex >= 0
                && occupantIndex < worldSnapshot.Obstacles.Count)
            {
                roofHazard = worldSnapshot.Obstacles[occupantIndex];
                return true;
            }

            roofHazard = null;
            return false;
        }

        private static bool TryFindRoofLandingContinuation(
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            out ObstacleSnapshot roofObstacle,
            out int roofObstacleIndex)
        {
            roofObstacle = null;
            roofObstacleIndex = -1;

            if (worldSnapshot == null || obstacle == null)
                return false;

            for (int candidateIndex = obstacleIndex + 1; candidateIndex < worldSnapshot.Obstacles.Count; candidateIndex++)
            {
                ObstacleSnapshot candidate = worldSnapshot.Obstacles[candidateIndex];
                if (candidate.IsBottomLine != obstacle.IsBottomLine)
                    continue;

                if (!ObstacleClassifier.IsObstacleWithRoof(candidate.ObstacleType))
                    continue;

                roofObstacle = candidate;
                roofObstacleIndex = candidateIndex;
                return true;
            }

            return false;
        }

        private static JumpObstacleData[] BuildObstacleData(WorldSnapshot worldSnapshot)
        {
            var obstacles = new JumpObstacleData[worldSnapshot.Obstacles.Count];
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                obstacles[obstacleIndex] = new JumpObstacleData(
                    obstacle.ObstacleType,
                    obstacle.IsBottomLine,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX);
            }

            return obstacles;
        }
    }
}