using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Perception
{
    /// <summary>
    /// Хранит данные об одном видимом препятствии.
    /// </summary>
    public sealed class ObstacleSnapshot
    {
        /// <summary>
        /// Создает snapshot препятствия для planning-слоя.
        /// </summary>
        public ObstacleSnapshot(
            int instanceId,
            ObstacleTypeEnum obstacleType,
            bool isTopLine,
            float leftX,
            float rightX,
            float centerX)
        {
            InstanceId = instanceId;
            ObstacleType = obstacleType;
            IsTopLine = isTopLine;
            LeftX = leftX;
            RightX = rightX;
            CenterX = centerX;
        }

        public int InstanceId { get; }
        public ObstacleTypeEnum ObstacleType { get; }
        public bool IsTopLine { get; }
        public bool IsBottomLine => !IsTopLine;
        public float LeftX { get; }
        public float RightX { get; }
        public float CenterX { get; }
    }
}
