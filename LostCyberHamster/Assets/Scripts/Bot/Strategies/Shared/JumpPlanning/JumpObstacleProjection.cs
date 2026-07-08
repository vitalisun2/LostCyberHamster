using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Преобразует planning snapshot obstacles в данные runtime jump resolver'а.
    /// </summary>
    internal static class JumpObstacleProjection
    {
        public static List<JumpObstacleData> BuildBase(WorldSnapshot projectedWorldSnapshot)
        {
            var obstacles = new List<JumpObstacleData>(projectedWorldSnapshot.Obstacles.Count);
            BuildBase(projectedWorldSnapshot, obstacles);
            return obstacles;
        }

        public static void BuildBase(
            WorldSnapshot projectedWorldSnapshot,
            List<JumpObstacleData> obstacles)
        {
            RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.JumpObstacleProjectionBuildBaseCalls);
            RuntimePerformanceDiagnostics.Count(
                RuntimePerformanceCounter.JumpObstacleProjectionBuildBaseItems,
                projectedWorldSnapshot.Obstacles.Count);

            obstacles.Clear();
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                obstacles.Add(new JumpObstacleData(
                    obstacle.ObstacleType,
                    obstacle.IsBottomLine,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX,
                    obstacle.InstanceId,
                    hasY: true,
                    obstacle.BottomY,
                    obstacle.TopY,
                    obstacle.IsRemovedInPlanning));
            }
        }

        public static void BuildShifted(
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            List<JumpObstacleData> shiftedObstacles)
        {
            RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.JumpObstacleProjectionBuildShiftedCalls);
            RuntimePerformanceDiagnostics.Count(
                RuntimePerformanceCounter.JumpObstacleProjectionBuildShiftedItems,
                baseObstacles.Count);

            shiftedObstacles.Clear();
            for (int obstacleIndex = 0; obstacleIndex < baseObstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = baseObstacles[obstacleIndex];
                shiftedObstacles.Add(new JumpObstacleData(
                    obstacle.Type,
                    obstacle.IsBottomLine,
                    obstacle.LeftX - fireShift,
                    obstacle.RightX - fireShift,
                    obstacle.CenterX - fireShift,
                    obstacle.InstanceId,
                    obstacle.HasY,
                    obstacle.BottomY,
                    obstacle.TopY,
                    obstacle.IsRemovedInPlanning));
            }
        }
    }
}
