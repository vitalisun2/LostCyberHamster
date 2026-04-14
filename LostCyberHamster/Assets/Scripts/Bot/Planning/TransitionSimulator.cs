using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class TransitionSimulator
    {
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, BotPerceptionSnapshot perceptionSnapshot)
        {
            RuntimeStateSnapshot runtimeState = planningState.RuntimeState;
            if (action.TargetBottomLine.HasValue)
                runtimeState = runtimeState.WithLine(action.TargetBottomLine.Value, isOnRoof: false);

            int nextObstacleIndex = perceptionSnapshot.VisibleObstacles.Count;
            for (int obstacleIndex = 0; obstacleIndex < perceptionSnapshot.VisibleObstacles.Count; obstacleIndex++)
            {
                VisibleObstacleSnapshot obstacle = perceptionSnapshot.VisibleObstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - action.CompletionWorldShift;
                if (projectedRightX > runtimeState.HamsterLeftX)
                {
                    nextObstacleIndex = obstacleIndex;
                    break;
                }
            }

            return new PlanningState(
                runtimeState,
                nextObstacleIndex,
                planningState.ProjectionX + action.CompletionWorldShift);
        }
    }
}
