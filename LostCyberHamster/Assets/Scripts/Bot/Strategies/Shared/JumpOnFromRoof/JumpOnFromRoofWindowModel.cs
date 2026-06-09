using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Хранит target и границы fire-window для roof-to-road jump-on действия.
    /// </summary>
    internal readonly struct JumpOnFromRoofWindowModel
    {
        public JumpOnFromRoofWindowModel(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            ObstacleSnapshot lastRoof,
            float firstFireShift,
            float lastFireShift,
            float selectedFireShift)
        {
            TargetObstacle = targetObstacle;
            TargetObstacleIndex = targetObstacleIndex;
            TargetObstacleChainIndex = targetObstacleChainIndex;
            LastRoof = lastRoof;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
            SelectedFireShift = selectedFireShift;
        }

        /// <summary>
        /// Obstacle, на который планируется напрыгивание с крыши.
        /// </summary>
        public ObstacleSnapshot TargetObstacle { get; }

        /// <summary>
        /// Индекс target obstacle в world snapshot.
        /// </summary>
        public int TargetObstacleIndex { get; }

        /// <summary>
        /// Индекс target obstacle внутри action chain.
        /// </summary>
        public int TargetObstacleChainIndex { get; }

        /// <summary>
        /// Последняя passive roof перед сходом на дорогу.
        /// </summary>
        public ObstacleSnapshot LastRoof { get; }

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
