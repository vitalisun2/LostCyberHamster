namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver
{
    /// <summary>
    /// Хранит границы chain препятствий для jump-over и выбранный fire shift.
    /// </summary>
    internal readonly struct JumpOverChainModel
    {
        public JumpOverChainModel(
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

        public bool ContainsObstacleIndex(int obstacleIndex)
        {
            return obstacleIndex >= FirstObstacleIndex && obstacleIndex <= LastObstacleIndex;
        }
    }
}
