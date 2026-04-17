using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningState
    {
        public PlanningState(HamsterSnapshot runtimeState, int nextObstacleIndex, float projectionWorldShift)
        {
            RuntimeState = runtimeState;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
        }

        public HamsterSnapshot RuntimeState { get; }
        public int NextObstacleIndex { get; }
        public float ProjectionWorldShift { get; }
        public bool IsOnBottomLine => RuntimeState.IsOnBottomLine;

        public static PlanningState FromSnapshot(WorldSnapshot perceptionSnapshot)
        {
            return new PlanningState(
                perceptionSnapshot.RuntimeState,
                nextObstacleIndex: 0,
                projectionWorldShift: 0f);
        }
    }
}
