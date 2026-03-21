using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Проецирует состояние планировщика после применения шага.
    /// Поддерживает SwitchLane и Jump проекции.
    /// </summary>
    public class StateProjector
    {
        private const float SwitchLaneReturnControlTravel =
            0.47f * Assets.Scripts.Consts.GameSpeedBase;
        private const float JumpLandingTravel = 3.8f;
        private const float LandingPostFactor = 0.4f;
        private const float PassedObstacleMargin = 0.4f;

        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            return Project(PlannerState.FromSnapshot(snapshot), step);
        }

        public StepProjectionResult Project(PlannerState state, BranchStep step)
        {
            var nextState = state.Clone();

            switch (step.Action)
            {
                case BotAction.SwitchLane:
                    ProjectSwitchLane(nextState, state, step);
                    break;
                case BotAction.Jump:
                    ProjectJump(nextState, step);
                    break;
            }

            RebuildRemainingObjects(state, nextState, step);

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = nextState,
                DebugReason = step.Reason
            };
        }

        private static void ProjectSwitchLane(PlannerState nextState, PlannerState previousState, BranchStep step)
        {
            nextState.HamsterOnBottom = !previousState.HamsterOnBottom;
            nextState.HamsterOnRoof = false;

            float advanceDistance = step.TargetObstacle.DistanceToHamster - step.ExecuteAtDistance;
            if (advanceDistance < 0f)
                advanceDistance = 0f;

            nextState.HamsterRightX += advanceDistance + SwitchLaneReturnControlTravel;
        }

        private static void ProjectJump(PlannerState nextState, BranchStep step)
        {
            nextState.HamsterOnRoof = false;
            nextState.Energy -= step.EnergyCost;
            if (nextState.Energy < 0)
                nextState.Energy = 0;

            nextState.HamsterRightX = step.TargetObstacle.RightX
                                    + (JumpLandingTravel * LandingPostFactor);
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

                if (step.Action == BotAction.Jump && obstacle.StableId == step.TargetObstacle.StableId)
                    continue;

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
