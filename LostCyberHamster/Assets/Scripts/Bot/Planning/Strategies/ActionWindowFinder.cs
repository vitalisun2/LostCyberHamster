using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const float _searchStep = 0.005f;
        private const float _searchEpsilon = 0.0001f;
        private const float _interiorSelectionRatio = 0.5f;

        /// <summary>
        /// Резолвит runtime-исход прыжка.
        /// </summary>
        internal delegate JumpResolveResult ResolveDelegate(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context);

        /// <summary>
        /// Ищет fire shift для jump-over.
        /// </summary>
        public static bool TryFindJumpOverFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float fireShift)
        {
            return TryFindExactJumpOutcomeFireShift(
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

            /// <summary>
            /// Ищет fire shift для super jump-over.
            /// </summary>
        public static bool TryFindSuperJumpOverFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireShift)
        {
            return TryFindExactJumpOutcomeFireShift(
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

            /// <summary>
            /// Ищет fire shift для jump on roof.
            /// </summary>
        public static bool TryFindJumpOnRoofFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float fireShift)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)));

            HamsterSnapshot hamster = planningState.Hamster;
            if (!TryGetRoofLandingSearchWindow(
                    hamster,
                    targetObstacle,
                    jumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                LogJumpOnRoofWindow(
                    "NO_WINDOW",
                    hamster,
                    targetObstacle,
                    targetObstacleIndex,
                    jumpTravel,
                    firstFireShift,
                    lastFireShift);

                fireShift = 0f;
                return false;
            }

            LogJumpOnRoofWindow(
                "WINDOW",
                hamster,
                targetObstacle,
                targetObstacleIndex,
                jumpTravel,
                firstFireShift,
                lastFireShift);

            return TryFindExactJumpOutcomeFireShiftInWindow(
                planningState,
                projectedWorldSnapshot,
                targetObstacleIndex,
                jumpTravel,
                HamsterStateEnum.JumpOnRoof,
                damageBigAliveWithoutYByReach: true,
                JumpOutcomeResolver.ResolveJump,
                firstFireShift,
                lastFireShift,
                diagnosticPrefix: "JumpOnRoof",
                targetObstacle,
                out fireShift);
        }

            /// <summary>
            /// Ищет fire shift для точного jump-исхода.
            /// </summary>
        private static bool TryFindExactJumpOutcomeFireShift(
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

            return TryFindExactJumpOutcomeFireShiftInWindow(
                planningState,
                projectedWorldSnapshot,
                targetObstacleIndex,
                actionTravel,
                expectedState,
                damageBigAliveWithoutYByReach,
                resolver,
                firstFireShift,
                lastFireShift,
                diagnosticPrefix: null,
                targetObstacle,
                out fireShift);
        }

        /// <summary>
        /// Сканирует заданное окно и ищет точный jump-исход.
        /// </summary>
        private static bool TryFindExactJumpOutcomeFireShiftInWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            int targetObstacleIndex,
            float actionTravel,
            HamsterStateEnum expectedState,
            bool damageBigAliveWithoutYByReach,
            ResolveDelegate resolver,
            float firstFireShift,
            float lastFireShift,
            string diagnosticPrefix,
            ObstacleSnapshot targetObstacle,
            out float fireShift)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (resolver, nameof(resolver)));

            // Затем детерминированно сканируем это окно, строим exact-safe интервалы и выбираем точку внутри последнего robust окна.
            HamsterSnapshot hamster = planningState.Hamster;
            List<JumpObstacleData> baseObstacles = BuildBaseObstacleData(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            var exactOutcomeIntervals = new List<SafeInterval>();

            bool isInsideExactInterval = false;
            float intervalStart = 0f;
            float previousShift = firstFireShift;

            for (float candidateFireShift = firstFireShift;
                  candidateFireShift <= lastFireShift + _searchEpsilon;
                  candidateFireShift += _searchStep)
            {
                float clampedFireShift = candidateFireShift > lastFireShift
                    ? lastFireShift
                    : candidateFireShift;

                bool isExactOutcome = IsExactJumpOutcomeAtShift(
                        hamster,
                        baseObstacles,
                        shiftedObstacles,
                        clampedFireShift,
                        actionTravel,
                        targetObstacleIndex,
                        expectedState,
                        damageBigAliveWithoutYByReach,
                        resolver);

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
                if (TrySelectInteriorFireShift(interval, out fireShift))
                {
                    LogExactOutcomeSelection(
                        diagnosticPrefix,
                        targetObstacle,
                        targetObstacleIndex,
                        interval,
                        fireShift);

                    return true;
                }
            }

            LogNoExactOutcomeInterval(
                diagnosticPrefix,
                targetObstacle,
                targetObstacleIndex,
                exactOutcomeIntervals.Count);

            fireShift = 0f;
            return false;
        }

        /// <summary>
        /// Выбирает внутреннюю точку safe-окна.
        /// </summary>
        private static bool TrySelectInteriorFireShift(SafeInterval interval, out float fireShift)
        {
            return interval.TrySelectInteriorPoint(
                lateBudget: 0f,
                _interiorSelectionRatio,
                out fireShift,
                _searchEpsilon);
        }

            /// <summary>
            /// Возвращает физически допустимое окно поиска.
            /// </summary>
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

        /// <summary>
        /// Возвращает окно поиска для посадки на roof obstacle с X-overlap.
        /// </summary>
        internal static bool TryGetRoofLandingSearchWindow(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            Guard.NotNull(
                (hamster, nameof(hamster)),
                (targetObstacle, nameof(targetObstacle)));

            firstFireShift = targetObstacle.LeftX - jumpTravel - hamster.HamsterRightX;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            float lastRoofOverlapFireShift = targetObstacle.RightX - hamster.HamsterLeftX;
            float latestBeforeGroundContactFireShift = targetObstacle.LeftX - hamster.HamsterRightX;

            lastFireShift = Math.Min(lastRoofOverlapFireShift, latestBeforeGroundContactFireShift);
            return lastFireShift > 0f && firstFireShift <= lastFireShift;
        }

        /// <summary>
        /// Возвращает правую границу road-small chain.
        /// </summary>
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

        /// <summary>
        /// Проверяет точный jump-исход на заданном shift.
        /// </summary>
        internal static bool IsExactJumpOutcomeAtShift(
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

        /// <summary>
        /// Проверяет результат для цепочки small obstacle.
        /// </summary>
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

        /// <summary>
        /// Строит базовые obstacle-данные для резолвера.
        /// </summary>
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

        /// <summary>
        /// Строит obstacle-данные после world shift.
        /// </summary>
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

        private static void LogJumpOnRoofWindow(
            string status,
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            float firstFireShift,
            float lastFireShift)
        {
            DebugManager.DiagLog(
                $"[JumpOnRoof {status}] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"targetLeft={Format(targetObstacle.LeftX)} targetRight={Format(targetObstacle.RightX)} " +
                $"hamsterLeft={Format(hamster.HamsterLeftX)} hamsterRight={Format(hamster.HamsterRightX)} " +
                $"jumpTravel={Format(jumpTravel)} first={Format(firstFireShift)} last={Format(lastFireShift)}");
        }

        private static void LogExactOutcomeSelection(
            string diagnosticPrefix,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            SafeInterval interval,
            float fireShift)
        {
            if (diagnosticPrefix == null)
                return;

            DebugManager.DiagLog(
                $"[{diagnosticPrefix} SELECT] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"intervalStart={Format(interval.Start)} intervalEnd={Format(interval.End)} " +
                $"fireShift={Format(fireShift)}");
        }

        private static void LogNoExactOutcomeInterval(
            string diagnosticPrefix,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int intervalCount)
        {
            if (diagnosticPrefix == null)
                return;

            DebugManager.DiagLog(
                $"[{diagnosticPrefix} NO_EXACT_INTERVAL] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"exactIntervals={intervalCount}");
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
