using System.Collections.Generic;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    public sealed class SwitchLaneStrategy : IPlanningStrategy
    {
        private const float SwitchLaneDecisionDuration = 0.45f;
        private const float SwitchLaneDecisionTravel = SwitchLaneDecisionDuration * Assets.Scripts.Consts.GameSpeedBase;
        private const float ExecutionLeadDistance = 0.18f;
        private const float LatestFireSafetyMargin = 0.05f;
        private const float FireSelectionMargin = 0.02f;

        public BotActionKind ActionKind => BotActionKind.Tap;

        public bool TryGenerate(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out PlannedAction action)
        {
            action = null;

            if (decisionPoint == null || decisionPoint.Kind != DecisionPointKind.BlockingGroundObstacle)
                return false;

            ObstacleSnapshot targetObstacle = decisionPoint.Obstacle;
            int targetObstacleIndex = decisionPoint.ObstacleIndex;
            if (!CanSwitchLane(planningState, targetObstacle))
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            float latestFireShift = targetObstacle.LeftX
                - hamster.HamsterRightX
                - LatestFireSafetyMargin
                - ExecutionLeadDistance;
            if (latestFireShift <= 0f)
                return false;

            if (!TryFindLatestSafeFireShift(worldSnapshot, hamster, !hamster.IsOnBottomLine, latestFireShift, out float fireShift))
                return false;

            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;
            action = new PlannedAction(
                BotActionKind.Tap,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + SwitchLaneDecisionTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: !hamster.IsOnBottomLine,
                energyCost: 0,
                description: $"Switch lane before {targetObstacle.ObstacleType}");
            return true;
        }

        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
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

        private static bool CanSwitchLane(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsDamaged || hamster.IsShifting)
                return false;

            return true;
        }

        private static bool TryFindLatestSafeFireShift(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift,
            out float fireShift)
        {
            var unsafeIntervals = CollectUnsafeFireIntervals(worldSnapshot, hamster, targetBottomLine, latestFireShift);
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
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            var unsafeIntervals = new List<UnsafeInterval>();

            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                float overlapStart = obstacle.LeftX - hamster.HamsterRightX;
                float overlapEnd = obstacle.RightX - hamster.HamsterLeftX;
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

        private static HamsterSnapshot ApplyActionToHamster(HamsterSnapshot hamster, PlannedAction action)
        {
            bool isOnBottomLine = action.TargetBottomLine ?? hamster.IsOnBottomLine;
            bool isOnRoof = action.TargetBottomLine.HasValue ? false : hamster.IsOnRoof;

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
