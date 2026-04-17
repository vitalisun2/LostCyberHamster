using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит прогнозируемое состояние бота внутри planning-дерева.
    /// </summary>
    public sealed class PlanningState
    {
        /// <summary>
        /// Создает одно planning-состояние для узла дерева решений.
        /// </summary>
        public PlanningState(HamsterSnapshot hamster, int nextObstacleIndex, float projectionWorldShift)
        {
            Hamster = hamster;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
        }

        public HamsterSnapshot Hamster { get; }
        public int NextObstacleIndex { get; }
        public float ProjectionWorldShift { get; }
        public bool IsOnBottomLine => Hamster.IsOnBottomLine;

        /// <summary>
        /// Создает корневое planning-состояние из snapshot мира.
        /// </summary>
        public static PlanningState FromSnapshot(WorldSnapshot worldSnapshot)
        {
            return new PlanningState(
                worldSnapshot.Hamster,
                nextObstacleIndex: 0,
                projectionWorldShift: 0f);
        }
    }
}
