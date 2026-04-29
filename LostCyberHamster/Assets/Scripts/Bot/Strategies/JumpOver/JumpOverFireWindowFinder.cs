using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Ищет fire shift для обычного jump-over.
    /// </summary>
    internal sealed class JumpOverFireWindowFinder
    {
        private const float _searchStep = 0.005f;
        private const float _searchEpsilon = 0.0001f;
        private const float _interiorSelectionRatio = 0.5f;

        #region Public API

        /// <summary>
        /// Подбирает fire shift внутри допустимого окна для jump-over.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)));

            if (!TryGetFireShiftSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    jumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                fireShift = 0f;
                return false;
            }

            List<JumpObstacleData> baseObstacles = BuildBaseObstacles(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            return TrySelectInteriorExpectedOutcomeFireShift(
                planningState.Hamster,
                baseObstacles,
                shiftedObstacles,
                jumpTravel,
                targetObstacleIndex,
                firstFireShift,
                lastFireShift,
                out fireShift);
        }

        #endregion

        #region Search Window

        /// <summary>
        /// Получает физически допустимое окно запуска jump-over.
        /// </summary>
        private static bool TryGetFireShiftSearchWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            float chainRightX = GetRoadSmallChainRightX(
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex);

            firstFireShift = chainRightX - hamster.HamsterLeftX - jumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = targetObstacle.LeftX - hamster.HamsterRightX;
            return lastFireShift >= 0f && firstFireShift <= lastFireShift;
        }

        /// <summary>
        /// Возвращает правую границу target obstacle или всей цепочки road small obstacles.
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

        #endregion

        #region Obstacle Projection

        /// <summary>
        /// Преобразует planning obstacles в immutable base данные runtime resolver'а.
        /// </summary>
        private static List<JumpObstacleData> BuildBaseObstacles(WorldSnapshot projectedWorldSnapshot)
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
        /// Строит obstacles в координатах момента fire shift.
        /// </summary>
        private static void BuildShiftedObstacles(
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

        #endregion

        #region Outcome Interval Search

        /// <summary>
        /// Выбирает внутренний fire shift из интервала, где runtime resolver даёт ожидаемый jump-over outcome.
        /// </summary>
        private bool TrySelectInteriorExpectedOutcomeFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float jumpTravel,
            int targetObstacleIndex,
            float firstFireShift,
            float lastFireShift,
            out float fireShift)
        {
            List<SafeInterval> expectedOutcomeIntervals = FindExpectedOutcomeIntervals(
                hamster,
                baseObstacles,
                shiftedObstacles,
                jumpTravel,
                targetObstacleIndex,
                firstFireShift,
                lastFireShift);

            return TrySelectInteriorFireShift(expectedOutcomeIntervals, out fireShift);
        }

        /// <summary>
        /// Находит все интервалы fire shift, на которых jump resolver попадает в нужный target.
        /// </summary>
        private List<SafeInterval> FindExpectedOutcomeIntervals(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float jumpTravel,
            int targetObstacleIndex,
            float firstFireShift,
            float lastFireShift)
        {
            var intervals = new List<SafeInterval>();
            bool isInsideInterval = false;
            float intervalStart = 0f;
            float previousShift = firstFireShift;

            for (float candidateFireShift = firstFireShift;
                  candidateFireShift <= lastFireShift + _searchEpsilon;
                  candidateFireShift += _searchStep)
            {
                float clampedFireShift = candidateFireShift > lastFireShift
                    ? lastFireShift
                    : candidateFireShift;

                if (IsExpectedJumpOverOutcomeAtShift(
                        hamster,
                        baseObstacles,
                        shiftedObstacles,
                        clampedFireShift,
                        jumpTravel,
                        targetObstacleIndex))
                {
                    if (!isInsideInterval)
                    {
                        intervalStart = clampedFireShift;
                        isInsideInterval = true;
                    }
                }
                else if (isInsideInterval)
                {
                    intervals.Add(new SafeInterval(intervalStart, previousShift));
                    isInsideInterval = false;
                }

                previousShift = clampedFireShift;
                if (clampedFireShift >= lastFireShift)
                    break;
            }

            if (isInsideInterval)
                intervals.Add(new SafeInterval(intervalStart, previousShift));

            return intervals;
        }

        /// <summary>
        /// Выбирает внутреннюю точку внутри последнего подходящего интервала.
        /// </summary>
        private static bool TrySelectInteriorFireShift(
            IReadOnlyList<SafeInterval> expectedOutcomeIntervals,
            out float fireShift)
        {
            for (int intervalIndex = expectedOutcomeIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                SafeInterval interval = expectedOutcomeIntervals[intervalIndex];
                if (interval.TrySelectInteriorPoint(
                        0f,
                        _interiorSelectionRatio,
                        out fireShift,
                        _searchEpsilon))
                {
                    return true;
                }
            }

            fireShift = 0f;
            return false;
        }

        #endregion

        #region Outcome Resolution

        /// <summary>
        /// Проверяет, что fire shift приводит ровно к JumpOver по ожидаемому obstacle.
        /// </summary>
        private static bool IsExpectedJumpOverOutcomeAtShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float jumpTravel,
            int targetObstacleIndex)
        {
            JumpResolveResult result = ResolveJumpAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                jumpTravel);

            return result.State == HamsterStateEnum.JumpOver
                   && IsTargetMatch(shiftedObstacles, targetObstacleIndex, result.TargetIndex);
        }

        /// <summary>
        /// Сдвигает obstacles в момент fire shift и запускает runtime jump resolver.
        /// </summary>
        private static JumpResolveResult ResolveJumpAtShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float jumpTravel)
        {
            BuildShiftedObstacles(baseObstacles, fireShift, shiftedObstacles);

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                jumpTravel,
                jumpTravel,
                damageBigAliveWithoutYByReach: true);

            return JumpOutcomeResolver.ResolveJump(shiftedObstacles, context);
        }

        #endregion

        #region Target Matching

        /// <summary>
        /// Проверяет прямое попадание в target или допустимый over-result по цепочке road small obstacles.
        /// </summary>
        private static bool IsTargetMatch(
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
            int targetObstacleIndex,
            int resolvedTargetIndex)
        {
            return resolvedTargetIndex == targetObstacleIndex
                   || IsRoadSmallChainOverResult(shiftedObstacles, targetObstacleIndex, resolvedTargetIndex);
        }

        /// <summary>
        /// Разрешает случай, когда resolver возвращает более поздний obstacle из одной цепочки road small obstacles.
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

        #endregion

        #region Types

        /// <summary>
        /// Хранит непрерывный интервал fire shifts с ожидаемым outcome.
        /// </summary>
        private readonly struct SafeInterval
        {
            public SafeInterval(float start, float end)
            {
                Start = start;
                End = end;
            }

            private float Start { get; }
            private float End { get; }

            public bool TrySelectInteriorPoint(
                float lateBudget,
                float selectionRatio,
                out float selectedPoint,
                float epsilon)
            {
                float effectiveEnd = End - lateBudget;
                if (effectiveEnd <= Start + epsilon)
                {
                    selectedPoint = 0f;
                    return false;
                }

                selectedPoint = Start + (effectiveEnd - Start) * selectionRatio;
                return true;
            }
        }

        #endregion
    }
}
