using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
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
        public readonly int StableId;

        public ObstacleInfo(
            ObstacleTypeEnum type,
            bool isTopLane,
            float leftX, float rightX, float centerX,
            float distanceToHamster,
            int stableId)
        {
            Type = type;
            IsTopLane = isTopLane;
            LeftX = leftX;
            RightX = rightX;
            CenterX = centerX;
            DistanceToHamster = distanceToHamster;
            StableId = stableId;
        }
    }
}
