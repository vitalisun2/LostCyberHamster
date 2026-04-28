using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Описывает текущую обязательную для обработки ситуацию перед ботом.
    /// </summary>
    public sealed class DecisionPoint
    {
        /// <summary>
        /// Создает новую точку решения для planning-слоя.
        /// </summary>
        public DecisionPoint(
            DecisionPointKind kind,
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            ObstacleSnapshot roofLandingObstacle = null,
            int roofLandingObstacleIndex = -1)
        {
            Kind = kind;
            Obstacle = obstacle;
            ObstacleIndex = obstacleIndex;
            RoofLandingObstacle = roofLandingObstacle;
            RoofLandingObstacleIndex = roofLandingObstacleIndex;
        }

        public DecisionPointKind Kind { get; }
        public ObstacleSnapshot Obstacle { get; }
        public int ObstacleIndex { get; }
        public ObstacleSnapshot RoofLandingObstacle { get; }
        public int RoofLandingObstacleIndex { get; }

        /// <summary>
        /// Возвращает true, если точка решения содержит явную roof-landing continuation.
        /// </summary>
        public bool HasRoofLandingObstacle => RoofLandingObstacle != null && RoofLandingObstacleIndex >= 0;
    }
}