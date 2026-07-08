using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Diagnostics;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Сдвигает snapshot мира в прогнозируемое будущее состояние.
    /// </summary>
    public static class PlanningSnapshotProjector
    {
        /// <summary>
        /// Проецирует препятствия из исходного snapshot с учетом world shift.
        /// </summary>
        public static WorldSnapshot Project(WorldSnapshot sourceSnapshot, PlanningState planningState)
        {
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.RuntimeBotPlanningSnapshotProjectorProject);
            try
            {
                if (sourceSnapshot == null || planningState == null)
                    return null;

                IReadOnlyList<ObstacleSnapshot> projectedObstacles =
                    planningState.ProjectionWorldShift == 0f
                    && planningState.RemovedObstacleInstanceIds.Count == 0
                        ? sourceSnapshot.Obstacles
                        : new ProjectedObstacleList(sourceSnapshot.Obstacles, planningState);

                return new WorldSnapshot(
                    planningState.Hamster,
                    projectedObstacles,
                    sourceSnapshot.ScreenLeftEdgeX,
                    sourceSnapshot.ScreenRightEdgeX,
                    sourceSnapshot.SnapshotTime);
            }
            finally
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.RuntimeBotPlanningSnapshotProjectorProject,
                    allocationSample);
            }
        }

        private sealed class ProjectedObstacleList : IReadOnlyList<ObstacleSnapshot>
        {
            private readonly IReadOnlyList<ObstacleSnapshot> _source;
            private readonly PlanningState _planningState;
            private readonly ObstacleSnapshot[] _cache;

            public ProjectedObstacleList(
                IReadOnlyList<ObstacleSnapshot> source,
                PlanningState planningState)
            {
                _source = source;
                _planningState = planningState;
                _cache = new ObstacleSnapshot[source.Count];
            }

            public int Count => _source.Count;

            public ObstacleSnapshot this[int index]
            {
                get
                {
                    ObstacleSnapshot projected = _cache[index];
                    if (projected != null)
                        return projected;

                    ObstacleSnapshot obstacle = _source[index];
                    float projectionWorldShift = _planningState.ProjectionWorldShift;
                    projected = new ObstacleSnapshot(
                        obstacle.InstanceId,
                        obstacle.ObstacleType,
                        obstacle.IsTopLine,
                        obstacle.LeftX - projectionWorldShift,
                        obstacle.RightX - projectionWorldShift,
                        obstacle.CenterX - projectionWorldShift,
                        obstacle.BottomY,
                        obstacle.TopY,
                        obstacle.IsRemovedInPlanning || _planningState.IsObstacleRemoved(obstacle.InstanceId));
                    _cache[index] = projected;
                    return projected;
                }
            }

            public IEnumerator<ObstacleSnapshot> GetEnumerator()
            {
                for (int index = 0; index < Count; index++)
                    yield return this[index];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
