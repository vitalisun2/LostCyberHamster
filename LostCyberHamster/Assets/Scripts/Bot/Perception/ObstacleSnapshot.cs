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
            float centerX,
            float bottomY,
            float topY,
            bool isRemovedInPlanning = false)
        {
            InstanceId = instanceId;
            ObstacleType = obstacleType;
            IsTopLine = isTopLine;
            LeftX = leftX;
            RightX = rightX;
            CenterX = centerX;
            BottomY = bottomY;
            TopY = topY;
            IsRemovedInPlanning = isRemovedInPlanning;
        }

        /// <summary>
        /// Runtime instance id obstacle.
        /// </summary>
        public int InstanceId { get; }

        /// <summary>
        /// Тип препятствия.
        /// </summary>
        public ObstacleTypeEnum ObstacleType { get; }

        /// <summary>
        /// Признак верхней линии.
        /// </summary>
        public bool IsTopLine { get; }

        /// <summary>
        /// Признак нижней линии.
        /// </summary>
        public bool IsBottomLine => !IsTopLine;

        /// <summary>
        /// Левая X-граница collider.
        /// </summary>
        public float LeftX { get; }

        /// <summary>
        /// Правая X-граница collider.
        /// </summary>
        public float RightX { get; }

        /// <summary>
        /// Центр collider по X.
        /// </summary>
        public float CenterX { get; }

        /// <summary>
        /// Нижняя Y-граница collider.
        /// </summary>
        public float BottomY { get; }

        /// <summary>
        /// Верхняя Y-граница collider.
        /// </summary>
        public float TopY { get; }

        /// <summary>
        /// Признак obstacle, который уже уничтожен внутри projected planning-ветки.
        /// </summary>
        public bool IsRemovedInPlanning { get; }
    }
}
