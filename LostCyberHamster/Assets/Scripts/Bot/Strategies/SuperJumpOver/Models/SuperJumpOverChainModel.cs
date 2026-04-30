namespace Assets.Scripts.Bot.Strategies.SuperJumpOver.Models
{
    /// <summary>
    /// Хранит границы цепочки препятствий для супер-прыжка и выбранные смещения по огню.
    /// </summary>
    internal readonly struct SuperJumpOverChainModel
    {
        public SuperJumpOverChainModel(
            int firstObstacleIndex,
            int lastObstacleIndex,
            int obstacleCount,
            float firstFireShift,
            float lastFireShift,
            float selectedFireShift)
        {
            FirstObstacleIndex = firstObstacleIndex;
            LastObstacleIndex = lastObstacleIndex;
            ObstacleCount = obstacleCount;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
            SelectedFireShift = selectedFireShift;
        }

        public int FirstObstacleIndex { get; }
        public int LastObstacleIndex { get; }
        public int ObstacleCount { get; }
        public float FirstFireShift { get; }
        public float LastFireShift { get; }
        public float SelectedFireShift { get; }

        /// <summary>
        /// Проверяет, входит ли индекс препятствия в диапазон текущей цепочки.
        /// </summary>
        public bool ContainsObstacleIndex(int obstacleIndex)
        {
            return obstacleIndex >= FirstObstacleIndex && obstacleIndex <= LastObstacleIndex;
        }
    }
}
