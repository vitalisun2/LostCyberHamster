using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Единый projector planner-состояния. Один и тот же метод должен работать
    /// для первого, второго и последующих шагов; меняется только входное состояние.
    /// </summary>
    public class StateProjector
    {
        private const float SwitchLaneReturnControlDuration = 0.47f;
        private const float SwitchLaneReturnControlTravel = SwitchLaneReturnControlDuration * Assets.Scripts.Consts.GameSpeedBase;
        private const float JumpLandingTravel = 3.8f;
        private const float SuperJumpLandingTravel = 4.6f;
        private const float LandingPostFactor = 0.4f;
        private const float JumpOnBounceTravel = 3.5f;
        private const float PassedObstacleMargin = 0.4f;

        public StepProjectionResult Project(BotSceneSnapshot snapshot, ChainStep step)
        {
            return Project(PlannerState.FromSnapshot(snapshot), step);
        }

        public StepProjectionResult Project(PlannerState state, ChainStep step)
        {
            var nextState = state.Clone();
            float previousRightX = state.HamsterRightX;

            ApplyStepOutcome(nextState, step);

            var result = new StepProjectionResult
            {
                IsSafe = true,
                NextState = nextState,
                EnergyDelta = nextState.Energy - state.Energy,
                DebugReason = step.Reason
            };

            RebuildRemainingObjects(state, nextState, step, previousRightX, result);
            return result;
        }

        private static void ApplyStepOutcome(PlannerState state, ChainStep step)
        {
            state.Energy -= step.EnergyCost;
            if (state.Energy < 0)
                state.Energy = 0;

            switch (step.Action)
            {
                case BotAction.SwitchLane:
                    ProjectSwitchLane(state, step);
                    return;

                case BotAction.Jump:
                case BotAction.SuperJump:
                    ProjectJump(state, step);
                    return;
            }
        }

        private static void ProjectSwitchLane(PlannerState state, ChainStep step)
        {
            state.HamsterOnBottom = !state.HamsterOnBottom;
            state.HamsterOnRoof = false;

            float advanceDistance = step.TargetObstacle.DistanceToHamster - step.ExecuteAtDistance;
            if (advanceDistance < 0f)
                advanceDistance = 0f;

            state.HamsterRightX += advanceDistance + SwitchLaneReturnControlTravel;
        }

        private static void ProjectJump(PlannerState state, ChainStep step)
        {
            if (step.Action == BotAction.SuperJump)
            {
                state.HamsterOnRoof = false;
                state.HamsterRightX = step.TargetObstacle.RightX + (SuperJumpLandingTravel * LandingPostFactor);
                return;
            }

            if (step.Semantic == StepSemantic.JumpOnBounce)
            {
                state.HamsterOnRoof = false;
                state.HamsterRightX = step.TargetObstacle.RightX + JumpOnBounceTravel;
                return;
            }

            if (step.Semantic == StepSemantic.JumpOnRoof)
            {
                state.HamsterOnRoof = true;
                state.HamsterRightX = step.TargetObstacle.LeftX + JumpLandingTravel;
                return;
            }

            state.HamsterOnRoof = false;
            state.HamsterRightX = step.TargetObstacle.RightX + (JumpLandingTravel * LandingPostFactor);
        }

        private static void RebuildRemainingObjects(
            PlannerState previousState,
            PlannerState nextState,
            ChainStep step,
            float previousRightX,
            StepProjectionResult result)
        {
            var remaining = new List<ObstacleInfo>();

            for (int i = 0; i < previousState.RemainingObjects.Count; i++)
            {
                var obstacle = previousState.RemainingObjects[i];

                if (obstacle.StableId == step.TargetObstacle.StableId && step.Action != BotAction.SwitchLane)
                {
                    result.ConsumedObjectIds.Add(obstacle.StableId);
                    continue;
                }

                bool wasPassed = obstacle.RightX < nextState.HamsterRightX - PassedObstacleMargin;
                bool wasCollected = IsCollectedDuringProjection(previousState, nextState, previousRightX, obstacle);

                if (wasCollected)
                {
                    result.CollectedObjects.Add(obstacle);
                    result.ConsumedObjectIds.Add(obstacle.StableId);
                    continue;
                }

                if (wasPassed)
                {
                    result.ConsumedObjectIds.Add(obstacle.StableId);
                    continue;
                }

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

        private static bool IsCollectedDuringProjection(
            PlannerState previousState,
            PlannerState nextState,
            float previousRightX,
            ObstacleInfo obstacle)
        {
            if (obstacle.Category != ObjectCategory.Collectible)
                return false;

            if (nextState.HamsterOnRoof)
                return false;

            bool collectibleOnBottom = !obstacle.IsTopLane;
            if (collectibleOnBottom != nextState.HamsterOnBottom)
                return false;

            bool overlapsTravelWindow =
                obstacle.RightX >= previousRightX - PassedObstacleMargin &&
                obstacle.LeftX <= nextState.HamsterRightX + PassedObstacleMargin;

            return overlapsTravelWindow;
        }
    }
}