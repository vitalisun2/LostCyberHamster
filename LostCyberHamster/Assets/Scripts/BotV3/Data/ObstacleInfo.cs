using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Снимок данных об одном видимом объекте сцены.
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
