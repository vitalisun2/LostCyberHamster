using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Ищет deterministic fire shift, который даёт ожидаемый runtime jump outcome.
    /// </summary>
    internal sealed class JumpOutcomeFireWindowCalculator
    {
        private const float SearchStep = 0.005f;
        private const float SearchEpsilon = 0.0001f;
        private const float InteriorSelectionRatio = 0.5f;
        private const float LateFireSafetyBudget = 0.1f;

        private readonly IJumpSearchWindowPolicy _searchWindowPolicy;
        private readonly IPreFireSafetyPolicy _preFireSafetyPolicy;
        private readonly HamsterStateEnum _expectedState;
        private readonly bool _damageBigAliveWithoutYByReach;
        private readonly JumpResolveDelegate _resolver;
        private readonly string _diagnosticPrefix;

        public JumpOutcomeFireWindowCalculator(
            IJumpSearchWindowPolicy searchWindowPolicy,
            HamsterStateEnum expectedState,
            bool damageBigAliveWithoutYByReach,
            JumpResolveDelegate resolver,
            IPreFireSafetyPolicy preFireSafetyPolicy = null,
            string diagnosticPrefix = null)
        {
            _searchWindowPolicy = searchWindowPolicy;
            _preFireSafetyPolicy = preFireSafetyPolicy;
            _expectedState = expectedState;
            _damageBigAliveWithoutYByReach = damageBigAliveWithoutYByReach;
            _resolver = resolver;
            _diagnosticPrefix = diagnosticPrefix;
        }

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            bool preferLatestFireShift,
            out float fireShift)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)),
                (_searchWindowPolicy, nameof(_searchWindowPolicy)),
                (_resolver, nameof(_resolver)));

            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    actionTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                LogWindow("NO_WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, actionTravel, firstFireShift, lastFireShift);
                fireShift = 0f;
                return false;
            }

            LogWindow("WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, actionTravel, firstFireShift, lastFireShift);
            return TryFindExactOutcomeFireShiftInWindow(
                planningState,
                projectedWorldSnapshot,
                targetObstacleIndex,
                actionTravel,
                firstFireShift,
                lastFireShift,
                targetObstacle,
                preferLatestFireShift,
                out fireShift);
        }

        public bool IsScheduledFireShiftStillValid(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            PlannedAction action,
            float validationEpsilon)
        {
            if (planningState == null || projectedWorldSnapshot == null || targetObstacle == null || action == null)
                return false;

            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            if (!TryGetRemainingFireShift(projectedWorldSnapshot, targetObstacle, action, out float fireShift))
                return false;

            if (fireShift < firstFireShift - validationEpsilon || fireShift > lastFireShift + validationEpsilon)
                return false;

            List<JumpObstacleData> baseObstacles = BuildBaseObstacleData(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            return IsFeasibleFireShift(
                planningState.Hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                action.PostFireWorldShift,
                targetObstacleIndex);
        }

        private bool TryFindExactOutcomeFireShiftInWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            int targetObstacleIndex,
            float actionTravel,
            float firstFireShift,
            float lastFireShift,
            ObstacleSnapshot targetObstacle,
            bool preferLatestFireShift,
            out float fireShift)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            List<JumpObstacleData> baseObstacles = BuildBaseObstacleData(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            var exactOutcomeIntervals = new List<SafeInterval>();

            bool isInsideExactInterval = false;
            float intervalStart = 0f;
            float previousShift = firstFireShift;

            for (float candidateFireShift = firstFireShift;
                  candidateFireShift <= lastFireShift + SearchEpsilon;
                  candidateFireShift += SearchStep)
            {
                float clampedFireShift = candidateFireShift > lastFireShift
                    ? lastFireShift
                    : candidateFireShift;

                bool isExactOutcome = IsFeasibleFireShift(
                    hamster,
                    baseObstacles,
                    shiftedObstacles,
                    clampedFireShift,
                    actionTravel,
                    targetObstacleIndex);

                if (isExactOutcome)
                {
                    if (!isInsideExactInterval)
                    {
                        intervalStart = clampedFireShift;
                        isInsideExactInterval = true;
                    }
                }
                else if (isInsideExactInterval)
                {
                    exactOutcomeIntervals.Add(new SafeInterval(intervalStart, previousShift));
                    isInsideExactInterval = false;
                }

                previousShift = clampedFireShift;
                if (clampedFireShift >= lastFireShift)
                    break;
            }

            if (isInsideExactInterval)
                exactOutcomeIntervals.Add(new SafeInterval(intervalStart, previousShift));

            for (int intervalIndex = exactOutcomeIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                SafeInterval interval = exactOutcomeIntervals[intervalIndex];
                float lateBudget = preferLatestFireShift ? LateFireSafetyBudget : 0f;
                float selectionRatio = preferLatestFireShift ? 1f : InteriorSelectionRatio;
                if (interval.TrySelectInteriorPoint(lateBudget, selectionRatio, out fireShift, SearchEpsilon))
                {
                    LogExactOutcomeSelection(targetObstacle, targetObstacleIndex, interval, fireShift);
                    LogResolvedOutcomeAtSelectedShift(
                        hamster,
                        baseObstacles,
                        shiftedObstacles,
                        targetObstacle,
                        targetObstacleIndex,
                        fireShift,
                        actionTravel);
                    return true;
                }
            }

            LogNoExactOutcomeInterval(targetObstacle, targetObstacleIndex, exactOutcomeIntervals.Count);
            fireShift = 0f;
            return false;
        }

        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            out float fireShift)
        {
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - action.TriggerX;
                    return true;
                }
            }

            fireShift = targetObstacle.LeftX - action.TriggerX;
            return targetObstacle != null;
        }

        private bool IsFeasibleFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex)
        {
            if (_preFireSafetyPolicy != null
                && !_preFireSafetyPolicy.CanWaitUntilFire(hamster, baseObstacles, fireShift))
            {
                return false;
            }

            return IsExactJumpOutcomeAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel,
                targetObstacleIndex);
        }

        private bool IsExactJumpOutcomeAtShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex)
        {
            BuildShiftedObstacleData(baseObstacles, fireShift, shiftedObstacles);

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                actionTravel,
                actionTravel,
                damageBigAliveWithoutYByReach: _damageBigAliveWithoutYByReach);

            JumpResolveResult result = _resolver(shiftedObstacles, context);
            if (result.State != _expectedState)
                return false;

            if (result.TargetIndex == targetObstacleIndex)
                return true;

            return IsRoadSmallChainOverResult(shiftedObstacles, targetObstacleIndex, result.TargetIndex);
        }

        private void LogResolvedOutcomeAtSelectedShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float actionTravel)
        {
            if (_diagnosticPrefix == null)
                return;

            BuildShiftedObstacleData(baseObstacles, fireShift, shiftedObstacles);

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                actionTravel,
                actionTravel,
                damageBigAliveWithoutYByReach: _damageBigAliveWithoutYByReach);

            JumpResolveResult result = _resolver(shiftedObstacles, context);
            bool directTargetMatch = result.TargetIndex == targetObstacleIndex;
            bool chainOverMatch = !directTargetMatch
                                  && IsRoadSmallChainOverResult(shiftedObstacles, targetObstacleIndex, result.TargetIndex);

            string resolvedTargetType = "none";
            if (result.TargetIndex >= 0 && result.TargetIndex < shiftedObstacles.Count)
                resolvedTargetType = shiftedObstacles[result.TargetIndex].Type.ToString();

            DebugManager.DiagLog(
                $"[{_diagnosticPrefix} RESOLVE] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"fireShift={Format(fireShift)} resolvedState={result.State} resolvedTargetIndex={result.TargetIndex} " +
                $"resolvedTargetType={resolvedTargetType} directTargetMatch={directTargetMatch} chainOverMatch={chainOverMatch}");
        }

        private static List<JumpObstacleData> BuildBaseObstacleData(WorldSnapshot projectedWorldSnapshot)
        {
            var obstacles = new List<JumpObstacleData>(projectedWorldSnapshot.Obstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                obstacles.Add(new JumpObstacleData(
                    obstacle.ObstacleType,
                    obstacle.IsBottomLine,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX));
            }

            return obstacles;
        }

        private static void BuildShiftedObstacleData(
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            List<JumpObstacleData> shiftedObstacles)
        {
            shiftedObstacles.Clear();
            for (int obstacleIndex = 0; obstacleIndex < baseObstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = baseObstacles[obstacleIndex];
                shiftedObstacles.Add(new JumpObstacleData(
                    obstacle.Type,
                    obstacle.IsBottomLine,
                    obstacle.LeftX - fireShift,
                    obstacle.RightX - fireShift,
                    obstacle.CenterX - fireShift,
                    obstacle.HasY,
                    obstacle.BottomY,
                    obstacle.TopY));
            }
        }

        private static bool IsRoadSmallChainOverResult(
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
            int targetObstacleIndex,
            int resolvedTargetIndex)
        {
            if (shiftedObstacles == null)
                return false;

            if (targetObstacleIndex < 0
                || resolvedTargetIndex < targetObstacleIndex
                || resolvedTargetIndex >= shiftedObstacles.Count)
            {
                return false;
            }

            JumpObstacleData targetObstacle = shiftedObstacles[targetObstacleIndex];
            if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(targetObstacle.Type))
                return false;

            bool isBottomLine = targetObstacle.IsBottomLine;
            for (int obstacleIndex = targetObstacleIndex; obstacleIndex <= resolvedTargetIndex; obstacleIndex++)
            {
                JumpObstacleData obstacle = shiftedObstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.Type))
                    continue;

                if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(obstacle.Type))
                    return false;
            }

            return true;
        }

        private void LogWindow(
            string status,
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            float firstFireShift,
            float lastFireShift)
        {
            if (_diagnosticPrefix == null)
                return;

            DebugManager.DiagLog(
                $"[{_diagnosticPrefix} {status}] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"targetLeft={Format(targetObstacle.LeftX)} targetRight={Format(targetObstacle.RightX)} " +
                $"hamsterLeft={Format(hamster.HamsterLeftX)} hamsterRight={Format(hamster.HamsterRightX)} " +
                $"actionTravel={Format(actionTravel)} first={Format(firstFireShift)} last={Format(lastFireShift)}");
        }

        private void LogExactOutcomeSelection(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            SafeInterval interval,
            float fireShift)
        {
            if (_diagnosticPrefix == null)
                return;

            DebugManager.DiagLog(
                $"[{_diagnosticPrefix} SELECT] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"intervalStart={Format(interval.Start)} intervalEnd={Format(interval.End)} " +
                $"fireShift={Format(fireShift)}");
        }

        private void LogNoExactOutcomeInterval(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int intervalCount)
        {
            if (_diagnosticPrefix == null)
                return;

            DebugManager.DiagLog(
                $"[{_diagnosticPrefix} NO_EXACT_INTERVAL] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"exactIntervals={intervalCount}");
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
