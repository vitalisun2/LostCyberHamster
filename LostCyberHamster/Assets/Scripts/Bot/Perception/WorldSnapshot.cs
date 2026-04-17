using System.Collections.Generic;

namespace Assets.Scripts.Bot.Perception
{
    public sealed class WorldSnapshot
    {
        public WorldSnapshot(
            HamsterSnapshot hamster,
            IReadOnlyList<ObstacleSnapshot> obstacles,
            float screenLeftEdgeX,
            float screenRightEdgeX,
            float visionRightEdgeX,
            float snapshotTime)
        {
            Hamster = hamster;
            Obstacles = obstacles;
            ScreenLeftEdgeX = screenLeftEdgeX;
            ScreenRightEdgeX = screenRightEdgeX;
            VisionRightEdgeX = visionRightEdgeX;
            SnapshotTime = snapshotTime;
        }

        public HamsterSnapshot Hamster { get; }
        public IReadOnlyList<ObstacleSnapshot> Obstacles { get; }
        public float ScreenLeftEdgeX { get; }
        public float ScreenRightEdgeX { get; }
        public float VisionRightEdgeX { get; }
        public float SnapshotTime { get; }
    }
}
