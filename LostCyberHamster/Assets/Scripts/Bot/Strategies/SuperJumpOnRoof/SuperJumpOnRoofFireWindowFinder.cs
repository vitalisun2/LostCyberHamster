using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Подбирает момент срабатывания super jump для посадки бота на целевую крышу.
    /// </summary>
    internal sealed class SuperJumpOnRoofFireWindowFinder
    {
        private const float _searchStep = 0.005f;
        private const float _searchEpsilon = 0.0001f;
        private const float _interiorSelectionRatio = 0.5f;
        private const float _lateFireSafetyBudget = 0.1f;

        /// <summary>
        /// Подбирает fire shift и target obstacle внутри допустимого окна для super-jump-on-roof chain.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            float superJumpTravel,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out float fireShift)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;
            fireShift = 0f;

            // Проверяет обязательные входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Находит валидную целевую крышу внутри chain.
            if (!TryGetRoofTarget(
                    planningState.Hamster,
                    chain,
                    out targetObstacle,
                    out targetObstacleIndex,
                    out int roofChainIndex))
            {
                return false;
            }

            // Вычисляет окно по старой super-jump-on-roof геометрии, чтобы сохранить поведение.
            if (!TryGetRoofLandingWindow(
                    planningState,
                    targetObstacle,
                    superJumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                LogWindow("NO_WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, superJumpTravel, firstFireShift, lastFireShift);
                return false;
            }

            // Сканирует окно и выбирает точку с exact runtime outcome.
            LogWindow("WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, superJumpTravel, firstFireShift, lastFireShift);
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            bool selected = TrySelectFireShift(
                planningState.Hamster,
                baseObstacles,
                targetObstacleIndex,
                superJumpTravel,
                firstFireShift,
                lastFireShift,
                preferLatestFireShift: roofChainIndex > 0,
                out fireShift,
                out FireShiftInterval selectedInterval,
                out int exactIntervalCount);

            if (!selected)
            {
                LogNoExactOutcomeInterval(targetObstacle, targetObstacleIndex, exactIntervalCount);
                return false;
            }

            // Пишет диагностику выбора и итогового runtime resolve.
            LogExactOutcomeSelection(targetObstacle, targetObstacleIndex, selectedInterval, fireShift);
            LogResolvedOutcomeAtSelectedShift(
                planningState.Hamster,
                baseObstacles,
                targetObstacle,
                targetObstacleIndex,
                fireShift,
                superJumpTravel);
            return true;
        }

        /// <summary>
        /// Находит первую доступную roof target внутри chain для super jump.
        /// </summary>
        internal static bool TryGetRoofTarget(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int roofChainIndex)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;
            roofChainIndex = -1;

            if (hamster == null || chain == null)
                return false;

            if (!chain.TryFindFirstRoof(out targetObstacle, out targetObstacleIndex, out roofChainIndex))
                return false;

            if (chain.HasDamagingRoofOccupant(roofChainIndex))
                return false;

            if (targetObstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            for (int chainIndex = 0; chainIndex < roofChainIndex; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleSnapshot obstacle, out _))
                    return false;

                if (obstacle.IsBottomLine != targetObstacle.IsBottomLine)
                    return false;

                if (!ObstacleClassifier.CanSuperJumpOverOnGround(obstacle.ObstacleType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Вычисляет допустимое окно fire shift для super-jump roof landing.
        /// </summary>
        internal static bool TryGetRoofLandingWindow(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            float actionTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            firstFireShift = 0f;
            lastFireShift = 0f;

            if (planningState == null || targetObstacle == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            firstFireShift = targetObstacle.LeftX - actionTravel - hamster.HamsterRightX;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            float lastRoofOverlapFireShift = targetObstacle.RightX - hamster.HamsterLeftX;
            float latestBeforeGroundContactFireShift = targetObstacle.LeftX - hamster.HamsterRightX;

            lastFireShift = global::System.Math.Min(lastRoofOverlapFireShift, latestBeforeGroundContactFireShift);
            return lastFireShift > 0f && firstFireShift <= lastFireShift;
        }

        /// <summary>
        /// Проверяет, что fire shift приводит к runtime outcome SuperJumpOnRoof по целевой крыше.
        /// </summary>
        internal static bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex)
        {
            var shiftedObstacles = new List<JumpObstacleData>(baseObstacles.Count);
            return IsExactOutcomeAtFireShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel,
                targetObstacleIndex);
        }

        /// <summary>
        /// Сканирует fire-window и выбирает точку внутри exact-outcome интервала.
        /// </summary>
        private static bool TrySelectFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int targetObstacleIndex,
            float actionTravel,
            float firstFireShift,
            float lastFireShift,
            bool preferLatestFireShift,
            out float fireShift,
            out FireShiftInterval selectedInterval,
            out int exactIntervalCount)
        {
            var exactOutcomeIntervals = new List<FireShiftInterval>();
            var shiftedObstacles = new List<JumpObstacleData>(baseObstacles.Count);
            bool isInsideExactInterval = false;
            float intervalStart = 0f;
            float previousShift = firstFireShift;

            // Собирает интервалы, где runtime resolver даёт точную посадку на целевую крышу.
            for (float candidateFireShift = firstFireShift;
                 candidateFireShift <= lastFireShift + _searchEpsilon;
                 candidateFireShift += _searchStep)
            {
                float clampedFireShift = candidateFireShift > lastFireShift
                    ? lastFireShift
                    : candidateFireShift;

                if (IsExactOutcomeAtFireShift(
                        hamster,
                        baseObstacles,
                        shiftedObstacles,
                        clampedFireShift,
                        actionTravel,
                        targetObstacleIndex))
                {
                    if (!isInsideExactInterval)
                    {
                        intervalStart = clampedFireShift;
                        isInsideExactInterval = true;
                    }
                }
                else if (isInsideExactInterval)
                {
                    exactOutcomeIntervals.Add(new FireShiftInterval(intervalStart, previousShift));
                    isInsideExactInterval = false;
                }

                previousShift = clampedFireShift;
                if (clampedFireShift >= lastFireShift)
                    break;
            }

            if (isInsideExactInterval)
                exactOutcomeIntervals.Add(new FireShiftInterval(intervalStart, previousShift));

            // Выбирает точку из последнего подходящего интервала по прежним правилам scanner'а.
            exactIntervalCount = exactOutcomeIntervals.Count;
            for (int intervalIndex = exactOutcomeIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                FireShiftInterval interval = exactOutcomeIntervals[intervalIndex];
                float lateBudget = preferLatestFireShift ? _lateFireSafetyBudget : 0f;
                float selectionRatio = preferLatestFireShift ? 1f : _interiorSelectionRatio;
                if (interval.TrySelectInteriorPoint(lateBudget, selectionRatio, out fireShift, _searchEpsilon))
                {
                    selectedInterval = interval;
                    return true;
                }
            }

            fireShift = 0f;
            selectedInterval = default;
            return false;
        }

        /// <summary>
        /// Проверяет точный runtime outcome в конкретной точке fire shift.
        /// </summary>
        private static bool IsExactOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex)
        {
            if (!JumpFireSafety.CanWaitUntilFire(hamster, baseObstacles, fireShift))
                return false;

            JumpResolveResult result = ResolveAtFireShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel);

            return result.State == HamsterStateEnum.SuperJumpOnRoof
                   && IsTargetMatch(shiftedObstacles, targetObstacleIndex, result.TargetIndex);
        }

        /// <summary>
        /// Вызывает runtime resolver для super jump в выбранной точке fire shift.
        /// </summary>
        private static JumpResolveResult ResolveAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel)
        {
            // Строит obstacle snapshot на момент fire.
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, shiftedObstacles);

            // Готовит runtime context для super jump.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                actionTravel,
                actionTravel,
                damageBigAliveWithoutYByReach: false);

            return SuperJumpOutcomeResolver.ResolveSuperJump(shiftedObstacles, context);
        }

        /// <summary>
        /// Проверяет совпадение runtime target с целевой крышей или допустимой road-small chain.
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
        /// Сохраняет старое правило совпадения для road-small chain-over результата.
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
        /// Пишет диагностику рассчитанного fire-window.
        /// </summary>
        private static void LogWindow(
            string status,
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            float firstFireShift,
            float lastFireShift)
        {
            DebugManager.DiagLog(
                $"[SuperJumpOnRoof {status}] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"targetLeft={Format(targetObstacle.LeftX)} targetRight={Format(targetObstacle.RightX)} " +
                $"hamsterLeft={Format(hamster.HamsterLeftX)} hamsterRight={Format(hamster.HamsterRightX)} " +
                $"actionTravel={Format(actionTravel)} first={Format(firstFireShift)} last={Format(lastFireShift)}");
        }

        /// <summary>
        /// Пишет диагностику выбранного exact-outcome интервала.
        /// </summary>
        private static void LogExactOutcomeSelection(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            FireShiftInterval interval,
            float fireShift)
        {
            DebugManager.DiagLog(
                $"[SuperJumpOnRoof SELECT] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"intervalStart={Format(interval.Start)} intervalEnd={Format(interval.End)} " +
                $"fireShift={Format(fireShift)}");
        }

        /// <summary>
        /// Пишет диагностику отсутствия подходящего exact-outcome интервала.
        /// </summary>
        private static void LogNoExactOutcomeInterval(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int intervalCount)
        {
            DebugManager.DiagLog(
                $"[SuperJumpOnRoof NO_EXACT_INTERVAL] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"exactIntervals={intervalCount}");
        }

        /// <summary>
        /// Пишет диагностику runtime resolve в выбранной точке.
        /// </summary>
        private static void LogResolvedOutcomeAtSelectedShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float actionTravel)
        {
            var shiftedObstacles = new List<JumpObstacleData>(baseObstacles.Count);
            JumpResolveResult result = ResolveAtFireShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel);

            bool directTargetMatch = result.TargetIndex == targetObstacleIndex;
            bool chainOverMatch = !directTargetMatch
                                  && IsTargetMatch(shiftedObstacles, targetObstacleIndex, result.TargetIndex);

            string resolvedTargetType = "none";
            if (result.TargetIndex >= 0 && result.TargetIndex < shiftedObstacles.Count)
                resolvedTargetType = shiftedObstacles[result.TargetIndex].Type.ToString();

            DebugManager.DiagLog(
                $"[SuperJumpOnRoof RESOLVE] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"fireShift={Format(fireShift)} resolvedState={result.State} resolvedTargetIndex={result.TargetIndex} " +
                $"resolvedTargetType={resolvedTargetType} directTargetMatch={directTargetMatch} chainOverMatch={chainOverMatch}");
        }

        /// <summary>
        /// Форматирует float для стабильных диагностических сообщений.
        /// </summary>
        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private readonly struct FireShiftInterval
        {
            public FireShiftInterval(float start, float end)
            {
                Start = start;
                End = end;
            }

            public float Start { get; }
            public float End { get; }

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
    }
}
