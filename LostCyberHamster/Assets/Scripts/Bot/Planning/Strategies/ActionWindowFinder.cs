using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    /// <summary>
    /// Ищет детерминированные окна запуска действий на основе shared outcome-resolver'ов.
    /// </summary>
    internal static class ActionWindowFinder
    {
        private const float SearchStep = 0.005f;
        private const float SearchEpsilon = 0.0001f;
        private const float InteriorSelectionRatio = 0.5f;
        private const float RuntimeFireDelayBudget = Assets.Scripts.Consts.GameSpeedBase / Assets.Scripts.Consts.FPS;

        internal delegate JumpResolveResult ResolveDelegate(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context);

        public static bool TryFindJumpOverFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float fireShift)
        {
            return TryFindExactOverFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                jumpTravel,
                HamsterStateEnum.JumpOver,
                damageBigAliveWithoutYByReach: true,
                JumpOutcomeResolver.ResolveJump,
                out fireShift);
        }

        public static bool TryFindSuperJumpOverFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireShift)
        {
            return TryFindExactOverFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                superJumpTravel,
                HamsterStateEnum.SuperJumpOver,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump,
                out fireShift);
        }

        private static bool TryFindExactOverFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            HamsterStateEnum expectedState,
            bool damageBigAliveWithoutYByReach,
            ResolveDelegate resolver,
            out float fireShift)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)),
                (resolver, nameof(resolver)));

            // Сначала ограничиваем поиск физически возможным окном старта.
            HamsterSnapshot hamster = planningState.Hamster;
            if (!TryGetSearchWindow(
                    hamster,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    actionTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                fireShift = 0f;
                return false;
            }

                        // Затем детерминированно сканируем это окно, строим exact-safe интервалы и выбираем точку внутри последнего robust окна.
            List<JumpObstacleData> baseObstacles = BuildBaseObstacleData(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
                        var exactSafeIntervals = new List<SafeInterval>();

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

                bool isExactOver = IsExactOverAtShift(
                        hamster,
                        baseObstacles,
                        shiftedObstacles,
                        clampedFireShift,
                        actionTravel,
                        targetObstacleIndex,
                        expectedState,
                        damageBigAliveWithoutYByReach,
                        resolver);

                if (isExactOver)
                {
                    if (!isInsideExactInterval)
                    {
                        intervalStart = clampedFireShift;
                        isInsideExactInterval = true;
                    }
                }
                else if (isInsideExactInterval)
                {
                    exactSafeIntervals.Add(new SafeInterval(intervalStart, previousShift));
                    isInsideExactInterval = false;
                }

                previousShift = clampedFireShift;

                if (clampedFireShift >= lastFireShift)
                    break;
            }

            if (isInsideExactInterval)
                exactSafeIntervals.Add(new SafeInterval(intervalStart, previousShift));

            for (int intervalIndex = exactSafeIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                SafeInterval interval = exactSafeIntervals[intervalIndex];
                if (TrySelectInteriorFireShift(interval, out fireShift))
                    return true;
            }

            fireShift = 0f;
            return false;
        }

        private static bool TrySelectInteriorFireShift(SafeInterval interval, out float fireShift)
        {
            return interval.TrySelectInteriorPoint(
                RuntimeFireDelayBudget,
                InteriorSelectionRatio,
                out fireShift,
                SearchEpsilon);
        }

        internal static bool TryGetSearchWindow(
            HamsterSnapshot hamster,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            float chainRightX = GetRoadSmallChainRightX(projectedWorldSnapshot, targetObstacle, targetObstacleIndex);
            firstFireShift = chainRightX - hamster.HamsterLeftX - actionTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = targetObstacle.LeftX - hamster.HamsterRightX;
            return lastFireShift >= 0f && firstFireShift <= lastFireShift;
        }

        private static float GetRoadSmallChainRightX(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex)
        {
            float chainRightX = targetObstacle.RightX;
            if (projectedWorldSnapshot == null
                || targetObstacleIndex < 0
                || targetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count
                || !ObstacleClassifier.IsRoadSmallOverChainObstacle(targetObstacle.ObstacleType))
            {
                return chainRightX;
            }

            bool isBottomLine = targetObstacle.IsBottomLine;
            for (int obstacleIndex = targetObstacleIndex + 1; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(obstacle.ObstacleType))
                    break;

                chainRightX = obstacle.RightX;
            }

            return chainRightX;
        }

        internal static bool IsExactOverAtShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex,
            HamsterStateEnum expectedState,
            bool damageBigAliveWithoutYByReach,
            ResolveDelegate resolver)
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
                damageBigAliveWithoutYByReach: damageBigAliveWithoutYByReach);

            JumpResolveResult result = resolver(shiftedObstacles, context);
            if (result.State != expectedState)
                return false;

            if (result.TargetIndex == targetObstacleIndex)
                return true;

            return IsRoadSmallChainOverResult(shiftedObstacles, targetObstacleIndex, result.TargetIndex);
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

        internal static List<JumpObstacleData> BuildBaseObstacleData(WorldSnapshot projectedWorldSnapshot)
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
    }
}