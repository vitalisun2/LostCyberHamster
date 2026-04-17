using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningState
    {
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

        public static PlanningState FromSnapshot(WorldSnapshot worldSnapshot)
        {
            return new PlanningState(
                worldSnapshot.Hamster,
                nextObstacleIndex: 0,
                projectionWorldShift: 0f);
        }
    }
}
