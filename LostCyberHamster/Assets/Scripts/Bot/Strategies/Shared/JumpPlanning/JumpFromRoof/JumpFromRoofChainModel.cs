using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Хранит obstacle chain и выбранный fire shift для прыжка с крыши на дорогу.
    /// </summary>
    internal readonly struct JumpFromRoofChainModel
    {
        public JumpFromRoofChainModel(
            ObstacleSnapshot firstObstacle,
            int firstObstacleIndex,
            ObstacleSnapshot lastObstacle,
            int lastObstacleIndex,
            int obstacleCount,
            float firstFireShift,
            float lastFireShift,
            float selectedFireShift)
        {
            FirstObstacle = firstObstacle;
            FirstObstacleIndex = firstObstacleIndex;
            LastObstacle = lastObstacle;
            LastObstacleIndex = lastObstacleIndex;
            ObstacleCount = obstacleCount;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
            SelectedFireShift = selectedFireShift;
        }

        /// <summary>
        /// Первый obstacle, покрываемый прыжком с крыши.
        /// </summary>
        public ObstacleSnapshot FirstObstacle { get; }

        /// <summary>
        /// Index первого obstacle в world snapshot.
        /// </summary>
        public int FirstObstacleIndex { get; }

        /// <summary>
        /// Последний obstacle, покрываемый прыжком с крыши.
        /// </summary>
        public ObstacleSnapshot LastObstacle { get; }

        /// <summary>
        /// Index последнего obstacle в world snapshot.
        /// </summary>
        public int LastObstacleIndex { get; }

        /// <summary>
        /// Количество obstacles, покрытых одним прыжком.
        /// </summary>
        public int ObstacleCount { get; }

        /// <summary>
        /// Левая граница fire window.
        /// </summary>
        public float FirstFireShift { get; }

        /// <summary>
        /// Правая граница fire window.
        /// </summary>
        public float LastFireShift { get; }

        /// <summary>
        /// Выбранный fire shift внутри окна.
        /// </summary>
        public float SelectedFireShift { get; }
    }
}
