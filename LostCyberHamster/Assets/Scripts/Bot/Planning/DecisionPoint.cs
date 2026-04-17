using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class DecisionPoint
    {
        public DecisionPoint(DecisionPointKind kind, ObstacleSnapshot obstacle, int obstacleIndex)
        {
            Kind = kind;
            Obstacle = obstacle;
            ObstacleIndex = obstacleIndex;
        }

        public DecisionPointKind Kind { get; }
        public ObstacleSnapshot Obstacle { get; }
        public int ObstacleIndex { get; }
    }
}
