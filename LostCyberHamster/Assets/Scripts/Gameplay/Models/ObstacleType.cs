using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Gameplay
{
    public class ObstacleType
    {
        public ObstacleTypeEnum ObstacleTypeEnum;
        public bool IsTop { get; }
        private string _isTopPositionString => IsTop ? "Top" : "Bottom";

        public ObstacleType(bool isTop, ObstacleTypeEnum obstacleTypeEnum)
        {
            ObstacleTypeEnum = obstacleTypeEnum;
            IsTop = isTop;
        }

        public override string ToString()
        {
            return $"{_isTopPositionString} {ObstacleTypeEnum}";
        }
    }
}
