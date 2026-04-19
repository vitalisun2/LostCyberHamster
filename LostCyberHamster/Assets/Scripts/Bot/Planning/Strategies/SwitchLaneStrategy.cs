using System.Collections.Generic;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using UnityEngine;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    /// <summary>
    /// Планирует смену линии перед blocking-препятствием на дороге.
    /// </summary>
    public sealed class SwitchLaneStrategy : IPlanningStrategy
    {
        private const float SwitchLaneDecisionDuration = 0.45f;
        private const float SwitchLaneDecisionTravel = SwitchLaneDecisionDuration * Assets.Scripts.Consts.GameSpeedBase;
        private const float ExecutionLeadDistance = 0.18f;
        private const float LatestFireSafetyMargin = 0.05f;
        private const float FireSelectionMargin = 0.02f;

        /// <summary>
        /// Возвращает тип действия, которое планирует стратегия.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.Tap;

        /// <summary>
        /// Добавляет кандидаты смены линии для текущей точки решения.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            if (actions == null)
                return;

            if (decisionPoint == null || decisionPoint.Kind != DecisionPointKind.BlockingGroundObstacle)
                return;

            ObstacleSnapshot targetObstacle = decisionPoint.Obstacle;
            int targetObstacleIndex = decisionPoint.ObstacleIndex;
            if (!CanSwitchLane(planningState, targetObstacle))
                return;

            HamsterSnapshot hamster = planningState.Hamster;
            float latestFireShift = targetObstacle.LeftX
                - hamster.HamsterRightX
                - LatestFireSafetyMargin
                - ExecutionLeadDistance;
            if (latestFireShift <= 0f)
                return;

            List<SafeInterval> safeIntervals = CollectSafeFireIntervals(
                worldSnapshot,
                hamster,
                !hamster.IsOnBottomLine,
                latestFireShift);
            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval interval = safeIntervals[intervalIndex];
                AddTapCandidate(actions, planningState, targetObstacle, targetObstacleIndex, interval.Start);

                if (Mathf.Abs(interval.End - interval.Start) > FireSelectionMargin)
                    AddTapCandidate(actions, planningState, targetObstacle, targetObstacleIndex, interval.End);
            }
        }

        private static void AddTapCandidate(
            List<PlannedAction> actions,
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift)
        {
            if (actions == null || planningState == null || targetObstacle == null)
                return;

            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;
            actions.Add(new PlannedAction(
                BotActionKind.Tap,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + SwitchLaneDecisionTravel,
                postFireWorldShift: SwitchLaneDecisionTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: !planningState.IsOnBottomLine,
                energyCost: 0,
                description: $"Switch lane before {targetObstacle.ObstacleType}"));
        }

        /// <summary>
        /// Симулирует состояние бота после успешной смены линии.
        /// </summary>
        public PlanningState Simulate(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyLaneSwitch(planningState.Hamster, action);
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        private static bool CanSwitchLane(PlanningState planningState, ObstacleSnapshot targetObstacle)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsDamaged || hamster.IsShifting)
                return false;

            return true;
        }

        private static List<SafeInterval> CollectSafeFireIntervals(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            var unsafeIntervals = CollectUnsafeFireIntervals(worldSnapshot, hamster, targetBottomLine, latestFireShift);
            unsafeIntervals.Sort((left, right) => left.Start.CompareTo(right.Start));

            var safeIntervals = new List<SafeInterval>();
            float safeStart = 0f;
            for (int intervalIndex = 0; intervalIndex < unsafeIntervals.Count; intervalIndex++)
            {
                UnsafeInterval interval = unsafeIntervals[intervalIndex];
                if (interval.End < safeStart)
                    continue;

                float safeEnd = interval.Start - FireSelectionMargin;
                if (safeEnd >= safeStart)
                    safeIntervals.Add(new SafeInterval(safeStart, safeEnd));

                if (interval.End + FireSelectionMargin > safeStart)
                    safeStart = interval.End + FireSelectionMargin;
            }

            if (safeStart <= latestFireShift)
                safeIntervals.Add(new SafeInterval(safeStart, latestFireShift));

            return safeIntervals;
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

    }
}
