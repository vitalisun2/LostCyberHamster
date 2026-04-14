using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    public sealed class SwitchLaneStrategy : IPlanningStrategy
    {
        private const float SwitchLaneDecisionDuration = 0.45f;
        private const float SwitchLaneDecisionTravel = SwitchLaneDecisionDuration * Assets.Scripts.Consts.GameSpeedBase;
        private const float ExecutionLeadDistance = 0.18f;
        private const float LatestFireSafetyMargin = 0.05f;
        private const float FireSelectionMargin = 0.02f;

        public bool TryGenerate(
            PlanningState planningState,
            BotPerceptionSnapshot perceptionSnapshot,
            VisibleObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            out PlannedAction action)
        {
            action = null;

            if (!CanSwitchLane(planningState, targetObstacle))
                return false;

            RuntimeStateSnapshot runtimeState = planningState.RuntimeState;
            float latestFireShift = targetObstacle.LeftX
                - runtimeState.HamsterRightX
                - LatestFireSafetyMargin
                - ExecutionLeadDistance;
            if (latestFireShift <= 0f)
                return false;

            if (!TryFindLatestSafeFireShift(perceptionSnapshot, runtimeState, !runtimeState.IsOnBottomLine, latestFireShift, out float fireShift))
                return false;

            float triggerX = targetObstacle.LeftX - fireShift;
            action = new PlannedAction(
                BotActionKind.Tap,
                triggerX,
                completionWorldShift: fireShift + SwitchLaneDecisionTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: !runtimeState.IsOnBottomLine,
                energyCost: 0,
                description: $"Switch lane before {targetObstacle.ObstacleType}");
            return true;
        }

        private static bool CanSwitchLane(PlanningState planningState, VisibleObstacleSnapshot targetObstacle)
        {
            RuntimeStateSnapshot runtimeState = planningState.RuntimeState;
            if (runtimeState.IsOnRoof || runtimeState.IsDamaged || runtimeState.IsShifting)
                return false;

            if (targetObstacle.ObstacleType == ObstacleTypeEnum.collectableCoin
                || targetObstacle.ObstacleType == ObstacleTypeEnum.collectableCrystal
                || targetObstacle.ObstacleType == ObstacleTypeEnum.collectableEnergetic
                || targetObstacle.ObstacleType == ObstacleTypeEnum.collectableLife
                || targetObstacle.ObstacleType == ObstacleTypeEnum.collectablePizza)
            {
                return false;
            }

            return true;
        }

        private static bool TryFindLatestSafeFireShift(
            BotPerceptionSnapshot perceptionSnapshot,
            RuntimeStateSnapshot runtimeState,
            bool targetBottomLine,
            float latestFireShift,
            out float fireShift)
        {
            var unsafeIntervals = CollectUnsafeFireIntervals(perceptionSnapshot, runtimeState, targetBottomLine, latestFireShift);
            unsafeIntervals.Sort((left, right) => left.Start.CompareTo(right.Start));

            float candidate = latestFireShift;
            for (int intervalIndex = unsafeIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                UnsafeInterval interval = unsafeIntervals[intervalIndex];
                if (candidate > interval.End)
                    continue;

                if (candidate >= interval.Start)
                    candidate = interval.Start - FireSelectionMargin;
            }

            fireShift = candidate;
            return fireShift >= 0f;
        }

        private static List<UnsafeInterval> CollectUnsafeFireIntervals(
            BotPerceptionSnapshot perceptionSnapshot,
            RuntimeStateSnapshot runtimeState,
            bool targetBottomLine,
            float latestFireShift)
        {
            var unsafeIntervals = new List<UnsafeInterval>();

            for (int obstacleIndex = 0; obstacleIndex < perceptionSnapshot.VisibleObstacles.Count; obstacleIndex++)
            {
                VisibleObstacleSnapshot obstacle = perceptionSnapshot.VisibleObstacles[obstacleIndex];
                if (!IsThreat(obstacle.ObstacleType))
                    continue;

                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                float overlapStart = obstacle.LeftX - runtimeState.HamsterRightX;
                float overlapEnd = obstacle.RightX - runtimeState.HamsterLeftX;
                float unsafeStart = overlapStart - SwitchLaneDecisionTravel;
                float unsafeEnd = overlapEnd;

                if (unsafeEnd < 0f || unsafeStart > latestFireShift)
                    continue;

                if (unsafeStart < 0f)
                    unsafeStart = 0f;

                if (unsafeEnd > latestFireShift)
                    unsafeEnd = latestFireShift;

                unsafeIntervals.Add(new UnsafeInterval(unsafeStart, unsafeEnd));
            }

            return unsafeIntervals;
        }

        private static bool IsThreat(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.smallAlive
                || obstacleType == ObstacleTypeEnum.bigAlive
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoad
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof
                || obstacleType == ObstacleTypeEnum.bigNotAlive
                || obstacleType == ObstacleTypeEnum.mediumNotAlive;
        }

        private readonly struct UnsafeInterval
        {
            public UnsafeInterval(float start, float end)
            {
                Start = start;
                End = end;
            }

            public float Start { get; }
            public float End { get; }
        }
    }
}
