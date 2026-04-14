using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningState
    {
        public PlanningState(RuntimeStateSnapshot runtimeState, int nextObstacleIndex, float projectionWorldShift)
        {
            RuntimeState = runtimeState;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
        }

        public RuntimeStateSnapshot RuntimeState { get; }
        public int NextObstacleIndex { get; }
        public float ProjectionWorldShift { get; }
        public bool IsOnBottomLine => RuntimeState.IsOnBottomLine;

        public static PlanningState FromSnapshot(BotPerceptionSnapshot perceptionSnapshot)
        {
            return new PlanningState(
                perceptionSnapshot.RuntimeState,
                nextObstacleIndex: 0,
                projectionWorldShift: 0f);
        }
    }
}
