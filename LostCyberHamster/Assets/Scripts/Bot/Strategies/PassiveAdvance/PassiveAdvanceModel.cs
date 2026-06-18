using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.PassiveAdvance
{
    /// <summary>
    /// Хранит рассчитанный no-input переход ожидания до следующей planning-точки.
    /// </summary>
    internal readonly struct PassiveAdvanceModel
    {
        public PassiveAdvanceModel(
            ObstacleSnapshot boundaryObstacle,
            int boundaryObstacleIndex,
            float completionWorldShift)
        {
            BoundaryObstacle = boundaryObstacle;
            BoundaryObstacleIndex = boundaryObstacleIndex;
            CompletionWorldShift = completionWorldShift;
        }

        /// <summary>
        /// Obstacle, после ухода которого можно продолжить анализ.
        /// </summary>
        public ObstacleSnapshot BoundaryObstacle { get; }

        /// <summary>
        /// World-index boundary obstacle в исходном snapshot.
        /// </summary>
        public int BoundaryObstacleIndex { get; }

        /// <summary>
        /// Сдвиг мира, после которого boundary obstacle уже не активен для planning.
        /// </summary>
        public float CompletionWorldShift { get; }
    }
}
