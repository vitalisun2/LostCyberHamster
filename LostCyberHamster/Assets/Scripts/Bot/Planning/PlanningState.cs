using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningState
    {
        public PlanningState(RuntimeStateSnapshot runtimeState, int nextObstacleIndex, float projectionX)
        {
            RuntimeState = runtimeState;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionX = projectionX;
        }

        public RuntimeStateSnapshot RuntimeState { get; }
        public int NextObstacleIndex { get; }
        public float ProjectionX { get; }

        public static PlanningState FromSnapshot(BotPerceptionSnapshot perceptionSnapshot)
        {
            return new PlanningState(
                perceptionSnapshot.RuntimeState,
                nextObstacleIndex: 0,
                projectionX: perceptionSnapshot.RuntimeState.HamsterRightX);
        }
    }
}
