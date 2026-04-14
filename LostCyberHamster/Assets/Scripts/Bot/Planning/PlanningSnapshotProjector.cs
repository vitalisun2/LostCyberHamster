using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public static class PlanningSnapshotProjector
    {
        public static BotPerceptionSnapshot Project(BotPerceptionSnapshot sourceSnapshot, PlanningState planningState)
        {
            if (sourceSnapshot == null || planningState == null)
                return null;

            var projectedObstacles = new List<VisibleObstacleSnapshot>(sourceSnapshot.VisibleObstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < sourceSnapshot.VisibleObstacles.Count; obstacleIndex++)
            {
                VisibleObstacleSnapshot obstacle = sourceSnapshot.VisibleObstacles[obstacleIndex];
                projectedObstacles.Add(new VisibleObstacleSnapshot(
                    obstacle.InstanceId,
                    obstacle.ObstacleType,
                    obstacle.IsTopLine,
                    obstacle.LeftX - planningState.ProjectionWorldShift,
                    obstacle.RightX - planningState.ProjectionWorldShift,
                    obstacle.CenterX - planningState.ProjectionWorldShift));
            }

            return new BotPerceptionSnapshot(
                planningState.RuntimeState,
                projectedObstacles,
                sourceSnapshot.ScreenLeftEdgeX,
                sourceSnapshot.ScreenRightEdgeX,
                sourceSnapshot.VisionRightEdgeX,
                sourceSnapshot.SnapshotTime);
        }
    }
}