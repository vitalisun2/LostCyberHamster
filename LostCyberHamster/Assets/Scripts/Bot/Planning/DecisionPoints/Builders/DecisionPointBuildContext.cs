using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Хранит общий вход для chain builders в рамках одного прохода detector'а.
    /// </summary>
    internal readonly struct DecisionPointBuildContext
    {
        public DecisionPointBuildContext(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            float maxFirstObstacleLeftX,
            float maxTargetLeftX)
        {
            PlanningState = planningState;
            WorldSnapshot = worldSnapshot;
            FirstObstacleIndex = firstObstacleIndex;
            MaxFirstObstacleLeftX = maxFirstObstacleLeftX;
            MaxTargetLeftX = maxTargetLeftX;
        }

        /// <summary>
        /// Projected-состояние хомяка и planner'а.
        /// </summary>
        public PlanningState PlanningState { get; }

        /// <summary>
        /// Projected snapshot мира.
        /// </summary>
        public WorldSnapshot WorldSnapshot { get; }

        /// <summary>
        /// Первый obstacle index, с которого builder должен начинать поиск.
        /// </summary>
        public int FirstObstacleIndex { get; }

        /// <summary>
        /// Самый правый допустимый left edge первого obstacle цепочки.
        /// </summary>
        public float MaxFirstObstacleLeftX { get; }

        /// <summary>
        /// Самый правый допустимый left edge target obstacle.
        /// </summary>
        public float MaxTargetLeftX { get; }

        /// <summary>
        /// Возвращает true, если контекст содержит минимально полный planning input.
        /// </summary>
        public bool HasValidInput => PlanningState?.Hamster != null
            && WorldSnapshot?.Obstacles != null;

        /// <summary>
        /// Snapshot хомяка из planning state.
        /// </summary>
        public HamsterSnapshot Hamster => PlanningState?.Hamster;
    }
}
