using System.Collections.Generic;

namespace Assets.Scripts.Bot.Perception
{
    public sealed class WorldSnapshot
    {
        public WorldSnapshot(
            HamsterSnapshot runtimeState,
            IReadOnlyList<ObstacleSnapshot> visibleObstacles,
            float screenLeftEdgeX,
            float screenRightEdgeX,
            float visionRightEdgeX,
            float snapshotTime)
        {
            RuntimeState = runtimeState;
            VisibleObstacles = visibleObstacles;
            ScreenLeftEdgeX = screenLeftEdgeX;
            ScreenRightEdgeX = screenRightEdgeX;
            VisionRightEdgeX = visionRightEdgeX;
            SnapshotTime = snapshotTime;
        }

        public HamsterSnapshot RuntimeState { get; }
        public IReadOnlyList<ObstacleSnapshot> VisibleObstacles { get; }
        public float ScreenLeftEdgeX { get; }
        public float ScreenRightEdgeX { get; }
        public float VisionRightEdgeX { get; }
        public float SnapshotTime { get; }
    }
}
