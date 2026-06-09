using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof
{
    /// <summary>
    /// Хранит выбранную roof target и границы fire-window.
    /// </summary>
    internal readonly struct JumpOnRoofWindowModel
    {
        public JumpOnRoofWindowModel(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            float firstFireShift,
            float lastFireShift)
        {
            TargetObstacle = targetObstacle;
            TargetObstacleIndex = targetObstacleIndex;
            TargetObstacleChainIndex = targetObstacleChainIndex;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
        }

        /// <summary>
        /// Roof support, на который планируется посадка.
        /// </summary>
        public ObstacleSnapshot TargetObstacle { get; }

        /// <summary>
        /// Индекс roof support в world snapshot.
        /// </summary>
        public int TargetObstacleIndex { get; }

        /// <summary>
        /// Индекс roof support внутри action chain.
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
    }
}
