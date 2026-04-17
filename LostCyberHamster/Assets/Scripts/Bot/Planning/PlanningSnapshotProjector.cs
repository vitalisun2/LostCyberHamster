using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public static class PlanningSnapshotProjector
    {
        public static WorldSnapshot Project(WorldSnapshot sourceSnapshot, PlanningState planningState)
        {
            if (sourceSnapshot == null || planningState == null)
                return null;

            var projectedObstacles = new List<ObstacleSnapshot>(sourceSnapshot.VisibleObstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < sourceSnapshot.VisibleObstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = sourceSnapshot.VisibleObstacles[obstacleIndex];
                projectedObstacles.Add(new ObstacleSnapshot(
                    obstacle.InstanceId,
                    obstacle.ObstacleType,
                    obstacle.IsTopLine,
                    obstacle.LeftX - planningState.ProjectionWorldShift,
                    obstacle.RightX - planningState.ProjectionWorldShift,
                    obstacle.CenterX - planningState.ProjectionWorldShift));
            }

            return new WorldSnapshot(
                planningState.RuntimeState,
                projectedObstacles,
                sourceSnapshot.ScreenLeftEdgeX,
                sourceSnapshot.ScreenRightEdgeX,
                sourceSnapshot.VisionRightEdgeX,
                sourceSnapshot.SnapshotTime);
        }
    }
}
