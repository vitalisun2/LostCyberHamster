using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Описывает текущую обязательную для обработки ситуацию перед ботом.
    /// </summary>
    public sealed class DecisionPoint
    {
        /// <summary>
        /// Создает новую точку решения для planning-слоя.
        /// </summary>
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
