using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class TransitionSimulator
    {
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            HamsterSnapshot nextHamster = ApplyActionToHamster(planningState.Hamster, action);
            float nextProjectionWorldShift = planningState.ProjectionWorldShift + action.CompletionWorldShift;

            int nextObstacleIndex = worldSnapshot.Obstacles.Count;
            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - nextProjectionWorldShift;
                if (projectedRightX > nextHamster.HamsterLeftX)
                {
                    nextObstacleIndex = obstacleIndex;
                    break;
                }
            }

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift);
        }

        private static HamsterSnapshot ApplyActionToHamster(HamsterSnapshot hamster, PlannedAction action)
        {
            // Apply line and roof changes produced by the completed action.
            bool isOnBottomLine = action.TargetBottomLine ?? hamster.IsOnBottomLine;
            bool isOnRoof = action.TargetBottomLine.HasValue ? false : hamster.IsOnRoof;

            // Keep projected resources in sync with the action cost.
            int energy = hamster.Energy - action.EnergyCost;
            if (energy < 0)
                energy = 0;

            return new HamsterSnapshot(
                hamster.HamsterState,
                isOnBottomLine,
                isOnRoof,
                energy,
                hamster.Lives,
                hamster.IsDamaged,
                isShifting: false,
                hamster.RoofSupportInstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);
        }
    }
}
