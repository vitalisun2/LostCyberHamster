using System.Collections.Generic;

namespace Assets.Scripts.Bot.Perception
{
    public sealed class BotPerceptionSnapshot
    {
        public BotPerceptionSnapshot(
            RuntimeStateSnapshot runtimeState,
            IReadOnlyList<VisibleObstacleSnapshot> visibleObstacles,
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

        public RuntimeStateSnapshot RuntimeState { get; }
        public IReadOnlyList<VisibleObstacleSnapshot> VisibleObstacles { get; }
        public float ScreenLeftEdgeX { get; }
        public float ScreenRightEdgeX { get; }
        public float VisionRightEdgeX { get; }
        public float SnapshotTime { get; }
    }
}
