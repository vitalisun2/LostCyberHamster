using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
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

                // Obstacles with roof выделяем в отдельную decision point, чтобы landing на крышу не смешивался с обычным ground block.
                if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                {
                    decisionPoint = new DecisionPoint(
                        DecisionPointKind.RoofLanding,
                        obstacle,
                        obstacleIndex);
                    return true;
                }

                decisionPoint = new DecisionPoint(
                    DecisionPointKind.BlockingGroundObstacle,
                    obstacle,
                    obstacleIndex);
                return true;
            }

            return false;
        }
    }
}
