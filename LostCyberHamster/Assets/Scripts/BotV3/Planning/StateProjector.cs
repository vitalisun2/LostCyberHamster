using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Проецирует состояние планировщика после применения шага.
    /// Поддерживает SwitchLane и Jump проекции.
    /// После проекции проверяет, не попадёт ли хомяк в threat на целевой позиции.
    /// </summary>
    public class StateProjector
    {
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

            bool safe = IsProjectedPositionSafe(state, nextState, step);

            return new StepProjectionResult
            {
                IsSafe = safe,
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

            nextState.HamsterRightX += advanceDistance + BotPhysicsConsts.SwitchLaneReturnControlTravel;
        }

        private static void ProjectJump(PlannerState nextState, BranchStep step)
        {
            nextState.HamsterOnRoof = false;
            nextState.Energy -= step.EnergyCost;
            if (nextState.Energy < 0)
                nextState.Energy = 0;

            nextState.HamsterRightX = step.TargetObstacle.RightX
                                    + (BotPhysicsConsts.JumpLandingOffset * LandingPostFactor);
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

        /// <summary>
        /// Проверяет, не окажется ли хомяк в зоне threat после проецированного шага.
        /// Для SwitchLane: проверяет target-lane threats в swept zone (transit + return control).
        /// Для Jump: проверяет same-lane threats в зоне приземления.
        /// </summary>
        private static bool IsProjectedPositionSafe(
            PlannerState previousState,
            PlannerState nextState,
            BranchStep step)
        {
            float hamsterLeftX = nextState.HamsterRightX - nextState.HamsterWidth;
            float hamsterRightX = nextState.HamsterRightX;

            for (int i = 0; i < previousState.RemainingObjects.Count; i++)
            {
                var obstacle = previousState.RemainingObjects[i];
                if (obstacle.StableId == step.TargetObstacle.StableId)
                    continue;
                if (!IsThreatType(obstacle.Type))
                    continue;

                bool obstacleOnBottom = !obstacle.IsTopLane;
                bool targetLaneIsBottom = nextState.HamsterOnBottom;
                if (obstacleOnBottom != targetLaneIsBottom)
                    continue;

                if (obstacle.RightX < hamsterLeftX - BotPhysicsConsts.SafetyPadding)
                    continue;

                if (CollisionUtils.IsOverlap(
                    hamsterLeftX - BotPhysicsConsts.SafetyPadding,
                    hamsterRightX + BotPhysicsConsts.SafetyPadding,
                    obstacle.LeftX,
                    obstacle.RightX))
                    return false;
            }

            return true;
        }

        private static bool IsThreatType(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallAlive:
                    return true;
                default:
                    return false;
            }
        }
    }
}
