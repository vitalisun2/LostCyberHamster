using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class TransitionSimulator
    {
        public PlanningState Simulate(PlanningState planningState, PlannedAction action)
        {
            return new PlanningState(
                planningState.RuntimeState,
                planningState.NextObstacleIndex,
                planningState.ProjectionX);
        }
    }
}
