using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class TransitionSimulator
    {
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, BotPerceptionSnapshot perceptionSnapshot)
        {
            if (planningState == null || action == null || perceptionSnapshot == null)
                return null;

            RuntimeStateSnapshot nextRuntimeState = ApplyActionToRuntimeState(planningState.RuntimeState, action);
            float nextProjectionWorldShift = planningState.ProjectionWorldShift + action.CompletionWorldShift;

            int nextObstacleIndex = perceptionSnapshot.VisibleObstacles.Count;
            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < perceptionSnapshot.VisibleObstacles.Count; obstacleIndex++)
            {
                VisibleObstacleSnapshot obstacle = perceptionSnapshot.VisibleObstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - nextProjectionWorldShift;
                if (projectedRightX > nextRuntimeState.HamsterLeftX)
                {
                    nextObstacleIndex = obstacleIndex;
                    break;
                }
            }

            return new PlanningState(
                nextRuntimeState,
                nextObstacleIndex,
                nextProjectionWorldShift);
        }

        private static RuntimeStateSnapshot ApplyActionToRuntimeState(RuntimeStateSnapshot runtimeState, PlannedAction action)
        {
            // Apply line and roof changes produced by the completed action.
            bool isOnBottomLine = action.TargetBottomLine ?? runtimeState.IsOnBottomLine;
            bool isOnRoof = action.TargetBottomLine.HasValue ? false : runtimeState.IsOnRoof;

            // Keep projected resources in sync with the action cost.
            int energy = runtimeState.Energy - action.EnergyCost;
            if (energy < 0)
                energy = 0;

            return new RuntimeStateSnapshot(
                runtimeState.HamsterState,
                isOnBottomLine,
                isOnRoof,
                energy,
                runtimeState.Lives,
                runtimeState.IsDamaged,
                isShifting: false,
                runtimeState.RoofSupportInstanceId,
                runtimeState.HamsterLeftX,
                runtimeState.HamsterRightX);
        }
    }
}
