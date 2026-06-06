using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoof
{
    /// <summary>
    /// Хранит covered road chain и fire-window для прыжка с крыши.
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
        /// Первый obstacle, покрываемый прыжком.
        /// </summary>
        public ObstacleSnapshot FirstObstacle { get; }

        /// <summary>
        /// Индекс первого obstacle в world snapshot.
        /// </summary>
        public int FirstObstacleIndex { get; }

        /// <summary>
        /// Последний obstacle, покрываемый прыжком.
        /// </summary>
        public ObstacleSnapshot LastObstacle { get; }

        /// <summary>
        /// Индекс последнего obstacle в world snapshot.
        /// </summary>
        public int LastObstacleIndex { get; }

        /// <summary>
        /// Количество obstacles, покрытых одним прыжком.
        /// </summary>
        public int ObstacleCount { get; }

        /// <summary>
        /// Левая граница fire-window.
        /// </summary>
        public float FirstFireShift { get; }

        /// <summary>
        /// Правая граница fire-window.
        /// </summary>
        public float LastFireShift { get; }

        /// <summary>
        /// Выбранный fire shift внутри окна.
        /// </summary>
        public float SelectedFireShift { get; }
    }
}
