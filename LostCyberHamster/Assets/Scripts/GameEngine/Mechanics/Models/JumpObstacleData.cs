using Assets.Scripts.Common.Models;

namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    public readonly struct JumpObstacleData
    {
        public readonly ObstacleTypeEnum Type;
        public readonly bool IsBottomLine;
        public readonly float LeftX;
        public readonly float RightX;
        public readonly float CenterX;
        public readonly bool HasY;
        public readonly float BottomY;
        public readonly float TopY;

        public JumpObstacleData(
            ObstacleTypeEnum type,
            bool isBottomLine,
            float leftX,
            float rightX,
            float centerX,
            bool hasY = false,
            float bottomY = 0f,
            float topY = 0f)
        {
            Type = type;
            IsBottomLine = isBottomLine;
            LeftX = leftX;
            RightX = rightX;
            CenterX = centerX;
            HasY = hasY;
            BottomY = bottomY;
            TopY = topY;
        }
    }
}
