using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Хранит границы fire-window для напрыгивания на дорожный smallAlive.
    /// </summary>
    internal readonly struct JumpOnWindowModel
    {
        public JumpOnWindowModel(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            float firstFireShift,
            float lastFireShift,
            float selectedFireShift)
        {
            TargetObstacle = targetObstacle;
            TargetObstacleIndex = targetObstacleIndex;
            TargetObstacleChainIndex = targetObstacleChainIndex;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
            SelectedFireShift = selectedFireShift;
        }

        /// <summary>
        /// Obstacle, на который планируется напрыгивание.
        /// </summary>
        public ObstacleSnapshot TargetObstacle { get; }

        /// <summary>
        /// Индекс target obstacle в world snapshot.
        /// </summary>
        public int TargetObstacleIndex { get; }

        /// <summary>
        /// Индекс target obstacle внутри chain.
        /// </summary>
        public int TargetObstacleChainIndex { get; }

        /// <summary>
        /// Левая граница допустимого fire shift.
        /// </summary>
        public float FirstFireShift { get; }

        /// <summary>
        /// Правая граница допустимого fire shift.
        /// </summary>
        public float LastFireShift { get; }

        /// <summary>
        /// Базовый выбранный fire shift внутри окна.
        /// </summary>
        public float SelectedFireShift { get; }
    }
}
