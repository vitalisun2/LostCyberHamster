using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Проецирует состояние планировщика после применения шага.
    /// V3 minimal: только SwitchLane проекция.
    /// </summary>
    public class StateProjector
    {
        private const float SwitchLaneReturnControlTravel =
            0.47f * Assets.Scripts.Consts.GameSpeedBase;
        private const float PassedObstacleMargin = 0.4f;

        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            return Project(PlannerState.FromSnapshot(snapshot), step);
        }

        public StepProjectionResult Project(PlannerState state, BranchStep step)
        {
            var nextState = state.Clone();

            nextState.HamsterOnBottom = !state.HamsterOnBottom;
            nextState.HamsterOnRoof = false;

            float advanceDistance = step.TargetObstacle.DistanceToHamster - step.ExecuteAtDistance;
            if (advanceDistance < 0f)
                advanceDistance = 0f;

            nextState.HamsterRightX += advanceDistance + SwitchLaneReturnControlTravel;

            RebuildRemainingObjects(state, nextState, step);

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = nextState,
                DebugReason = step.Reason
            };
        }

        private static void RebuildRemainingObjects(
            PlannerState previousState,
            PlannerState nextState,
            BranchStep step)
        {
            var remaining = new List<ObstacleInfo>();

            for (int i = 0; i < previousState.RemainingObjects.Count; i++)
            {
                var obstacle = previousState.RemainingObjects[i];

                bool wasPassed = obstacle.RightX < nextState.HamsterRightX - PassedObstacleMargin;
                if (wasPassed)
                    continue;

                float newDistance = obstacle.LeftX - nextState.HamsterRightX;
                remaining.Add(new ObstacleInfo(
                    obstacle.Type,
                    obstacle.IsTopLane,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX,
                    newDistance,
                    ObjectCategory.Neutral,
                    obstacle.StableId));
            }

            nextState.RemainingObjects = remaining;
        }
    }
}
