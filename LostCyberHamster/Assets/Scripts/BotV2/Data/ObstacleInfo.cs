using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Снимок данных об одном видимом объекте сцены.
    /// Без ObstacleRef — live-дистанция получается через StableId из ObstacleSpawner.
    /// </summary>
    public readonly struct ObstacleInfo
    {
        public readonly ObstacleTypeEnum Type;
        public readonly bool IsTopLane;
        public readonly float LeftX;
        public readonly float RightX;
        public readonly float CenterX;
        public readonly float DistanceToHamster;
        public readonly ObjectCategory Category;
        /// <summary>Obstacle.GetInstanceID() для поиска живого объекта в SpawnedObstacles.</summary>
        public readonly int StableId;

        public ObstacleInfo(
            ObstacleTypeEnum type,
            bool isTopLane,
            float leftX, float rightX, float centerX,
            float distanceToHamster,
            ObjectCategory category,
            int stableId)
        {
            Type = type;
            IsTopLane = isTopLane;
            LeftX = leftX;
            RightX = rightX;
            CenterX = centerX;
            DistanceToHamster = distanceToHamster;
            Category = category;
            StableId = stableId;
        }
    }
}
