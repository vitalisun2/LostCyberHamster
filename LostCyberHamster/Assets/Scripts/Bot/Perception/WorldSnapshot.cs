using System.Collections.Generic;

namespace Assets.Scripts.Bot.Perception
{
    /// <summary>
    /// Объединяет состояние хомяка и видимые препятствия в один снимок мира.
    /// </summary>
    public sealed class WorldSnapshot
    {
        /// <summary>
        /// Создает полный snapshot мира для planning-слоя.
        /// </summary>
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
