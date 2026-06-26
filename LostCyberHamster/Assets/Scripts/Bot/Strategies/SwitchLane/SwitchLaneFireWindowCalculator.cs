using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Timing;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Рассчитывает safe fire shifts для смены линии.
    /// </summary>
    internal sealed class SwitchLaneFireWindowCalculator
    {
        /// <summary>
        /// Вычисляет самый поздний допустимый fire shift для смены линии перед obstacle.
        /// </summary>
        public bool TryGetLatestFireShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            out float latestFireShift)
        {
            // Рассчитывает верхнюю границу окна запуска.
            latestFireShift = targetObstacle.LeftX
                - hamster.HamsterRightX
                - SwitchLaneTiming.ExecutionLeadDistance;

            // Возвращает признак существования допустимого окна.
            return latestFireShift > 0f;
        }

        /// <summary>
        /// Ограничивает latest fire shift ближайшей damaging-угрозой на текущей линии хомяка.
        /// </summary>
        /// <remarks>
        /// Этот scan нужен для opposite-lane entry сценариев, где trigger obstacle лежит на целевой
        /// линии и сам по себе не защищает от столкновения на текущей линии. Caller должен применять
        /// метод только когда trigger не является current-lane damaging obstacle; иначе deadline уже
        /// рассчитан по самому trigger-у через `TryGetLatestFireShift`.
        /// </remarks>
        public bool TryConstrainLatestFireShiftByCurrentLaneThreats(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            float latestFireShift,
            out float constrainedLatestFireShift,
            out string deadEndReason)
        {
            constrainedLatestFireShift = latestFireShift;
            deadEndReason = null;
            if (worldSnapshot?.Obstacles == null || hamster == null)
                return false;

            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle == null
                    || obstacle.IsRemovedInPlanning
                    || obstacle.IsBottomLine != hamster.IsOnBottomLine
                    || !ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                {
                    continue;
                }

                if (obstacle.RightX <= hamster.HamsterLeftX)
                    continue;

                float latestBeforeCurrentLaneCollision = obstacle.LeftX
                    - hamster.HamsterRightX
                    - SwitchLaneTiming.ExecutionLeadDistance;

                if (latestBeforeCurrentLaneCollision <= 0f)
                {
                    deadEndReason = "Нет безопасного окна для смены линии: текущая линия уже перекрыта опасным препятствием.";
                    return false;
                }

                if (latestBeforeCurrentLaneCollision < constrainedLatestFireShift)
                    constrainedLatestFireShift = latestBeforeCurrentLaneCollision;
            }

            if (constrainedLatestFireShift <= 0f)
            {
                deadEndReason = "Нет безопасного окна для смены линии: ближайшая угроза текущей линии наступает раньше запуска.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Выбирает ранний запуск в последнем безопасном окне перед deadline trigger obstacle.
        /// </summary>
        public bool TrySelectRelevantFireWindowSample(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift,
            out SwitchLaneFireWindowSample sample,
            bool requireTargetRoofSupport = true)
        {
            sample = default;

            // Собирает все безопасные интервалы запуска.
            List<SafeInterval> safeIntervals = CollectSafeFireIntervals(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift,
                requireTargetRoofSupport);

            // Берет пригодное окно, ближайшее к deadline trigger obstacle, и запускает смену линии в начале этого окна.
            return TrySelectLatestUsableSafeIntervalSample(safeIntervals, out sample);
        }

        /// <summary>
        /// Ищет самый поздний безопасный интервал, из которого можно выбрать точку запуска.
        /// </summary>
        private static bool TrySelectLatestUsableSafeIntervalSample(
            IReadOnlyList<SafeInterval> safeIntervals,
            out SwitchLaneFireWindowSample sample)
        {
            sample = default;
            if (safeIntervals == null || safeIntervals.Count == 0)
                return false;

            bool hasSelectedInterval = false;
            SafeInterval selectedInterval = default;
            SwitchLaneFireWindowSample selectedSample = default;
            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval candidate = safeIntervals[intervalIndex];
                if (!TryCreateFireWindowSample(
                        candidate,
                        SwitchLaneTiming.EarlyWindowSelectionRatio,
                        out SwitchLaneFireWindowSample candidateSample))
                {
                    continue;
                }

                if (!hasSelectedInterval
                    || candidate.End > selectedInterval.End
                    || (Math.Abs(candidate.End - selectedInterval.End) <= 0.001f
                        && candidate.Start > selectedInterval.Start))
                {
                    selectedInterval = candidate;
                    selectedSample = candidateSample;
                    hasSelectedInterval = true;
                }
            }

            if (!hasSelectedInterval)
                return false;

            sample = selectedSample;
            return true;
        }

        /// <summary>
        /// Создает sample из выбранного безопасного интервала.
        /// </summary>
        private static bool TryCreateFireWindowSample(
            SafeInterval interval,
            float selectionRatio,
            out SwitchLaneFireWindowSample sample)
        {
            sample = default;
            float paddedStart = interval.Start + SwitchLaneTiming.FireWindowBoundaryMargin;
            float paddedEnd = interval.End - SwitchLaneTiming.FireWindowBoundaryMargin;
            if (paddedEnd <= paddedStart)
                return false;

            var paddedInterval = new SafeInterval(paddedStart, paddedEnd);
            if (!paddedInterval.TrySelectInteriorPoint(
                    lateBudget: 0f,
                    selectionRatio,
                    out float fireShift,
                    epsilon: 0f))
            {
                return false;
            }

            sample = new SwitchLaneFireWindowSample(fireShift, paddedInterval.Start, paddedInterval.End);
            return true;
        }

        /// <summary>
        /// Строит безопасные интервалы запуска смены линии до заданной верхней границы.
        /// </summary>
        public List<SafeInterval> CollectSafeFireIntervals(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift,
            bool requireTargetRoofSupport = true)
        {
            // Собирает и упорядочивает все опасные интервалы.
            var unsafeIntervals = CollectUnsafeFireIntervals(worldSnapshot, hamster, targetBottomLine, latestFireShift);
            unsafeIntervals.Sort((left, right) => left.Start.CompareTo(right.Start));

            // Вычитает опасные интервалы из полного окна запуска.
            var safeIntervals = new List<SafeInterval>();
            float safeStart = 0f;
            for (int intervalIndex = 0; intervalIndex < unsafeIntervals.Count; intervalIndex++)
            {
                UnsafeInterval interval = unsafeIntervals[intervalIndex];
                if (interval.End < safeStart)
                    continue;

                float safeEnd = interval.Start;
                if (safeEnd >= safeStart)
                    safeIntervals.Add(new SafeInterval(safeStart, safeEnd));

                if (interval.End > safeStart)
                    safeStart = interval.End;
            }

            // Добавляет хвостовой безопасный интервал после последнего overlap.
            if (safeStart <= latestFireShift)
                safeIntervals.Add(new SafeInterval(safeStart, latestFireShift));

            // Для switch с крыши оставляет только окна, где target-линия имеет roof support под хомяком.
            if (hamster.IsOnRoof && requireTargetRoofSupport)
            {
                safeIntervals = IntersectWithTargetRoofSupportIntervals(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    safeIntervals);
            }

            return safeIntervals;
        }

        /// <summary>
        /// Ищет roof support на целевой линии в момент запуска SwitchLane.
        /// </summary>
        public bool TryFindTargetRoofSupportAtFireShift(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float fireShift,
            out ObstacleSnapshot support)
        {
            // Подготавливает результат и проверяет вход.
            support = null;
            if (worldSnapshot == null || hamster == null)
                return false;

            // Ищет roof, которую runtime найдет под хомяком сразу после tap.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (!IsTargetLineRoof(obstacle, targetBottomLine))
                    continue;

                if (!IsRoofSupportUnderHamsterAtFireShift(hamster, obstacle, fireShift))
                    continue;

                support = obstacle;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Собирает интервалы запуска, в которых смена линии приводит к пересечению с опасными obstacle.
        /// </summary>
        private static List<UnsafeInterval> CollectUnsafeFireIntervals(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            // Подготавливает накопитель опасных интервалов.
            var unsafeIntervals = new List<UnsafeInterval>();

            // Обходит obstacle на целевой линии и строит их overlap-окна.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                float overlapStart = obstacle.LeftX - hamster.HamsterRightX;
                float overlapEnd = obstacle.RightX - hamster.HamsterLeftX;
                float unsafeStart = overlapStart
                    - SwitchLaneTiming.DecisionTravel
                    - SwitchLaneTiming.PostActionTargetLaneGuardTravel;
                float unsafeEnd = overlapEnd;

                if (unsafeEnd < 0f || unsafeStart > latestFireShift)
                    continue;

                if (unsafeStart < 0f)
                    unsafeStart = 0f;

                if (unsafeEnd > latestFireShift)
                    unsafeEnd = latestFireShift;

                unsafeIntervals.Add(new UnsafeInterval(unsafeStart, unsafeEnd));
            }

            // Возвращает найденные опасные интервалы запуска.
            return unsafeIntervals;
        }

        /// <summary>
        /// Пересекает безопасные интервалы SwitchLane с интервалами наличия roof support на целевой линии.
        /// </summary>
        private static List<SafeInterval> IntersectWithTargetRoofSupportIntervals(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift,
            IReadOnlyList<SafeInterval> safeIntervals)
        {
            // Собирает интервалы, где target roof находится под центром хомяка с runtime tolerance.
            List<SafeInterval> supportIntervals = CollectTargetRoofSupportIntervals(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift);
            supportIntervals.Sort((left, right) => left.Start.CompareTo(right.Start));

            // Возвращает пересечение safety и roof-support окон.
            var supportedSafeIntervals = new List<SafeInterval>();
            for (int safeIndex = 0; safeIndex < safeIntervals.Count; safeIndex++)
            {
                SafeInterval safeInterval = safeIntervals[safeIndex];
                for (int supportIndex = 0; supportIndex < supportIntervals.Count; supportIndex++)
                {
                    SafeInterval supportInterval = supportIntervals[supportIndex];
                    float start = Math.Max(safeInterval.Start, supportInterval.Start);
                    float end = Math.Min(safeInterval.End, supportInterval.End);
                    if (start <= end)
                        supportedSafeIntervals.Add(new SafeInterval(start, end));
                }
            }

            return supportedSafeIntervals;
        }

        /// <summary>
        /// Собирает интервалы fire shift, в которых target-line roof остаётся под хомяком после tap.
        /// </summary>
        private static List<SafeInterval> CollectTargetRoofSupportIntervals(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            // Отсекает неполный вход.
            var supportIntervals = new List<SafeInterval>();
            if (worldSnapshot == null || hamster == null)
                return supportIntervals;

            // Проецирует каждую target-line roof в окно shift, где runtime считает её опорой.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (!IsTargetLineRoof(obstacle, targetBottomLine))
                    continue;

                float supportRadius = GetRoofSupportRadius(obstacle);
                float supportStart = obstacle.CenterX - hamster.CenterX - supportRadius;
                float supportEnd = obstacle.CenterX - hamster.CenterX + supportRadius;

                if (supportEnd < 0f || supportStart > latestFireShift)
                    continue;

                if (supportStart < 0f)
                    supportStart = 0f;

                if (supportEnd > latestFireShift)
                    supportEnd = latestFireShift;

                supportIntervals.Add(new SafeInterval(supportStart, supportEnd));
            }

            return supportIntervals;
        }

        /// <summary>
        /// Проверяет, является ли obstacle roof на целевой линии SwitchLane.
        /// </summary>
        private static bool IsTargetLineRoof(
            ObstacleSnapshot obstacle,
            bool targetBottomLine)
        {
            return obstacle != null
                && !obstacle.IsRemovedInPlanning
                && obstacle.IsBottomLine == targetBottomLine
                && ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
        }

        /// <summary>
        /// Проверяет runtime-условие поиска roof support под хомяком после сдвига мира.
        /// </summary>
        private static bool IsRoofSupportUnderHamsterAtFireShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot roof,
            float fireShift)
        {
            float shiftedRoofCenterX = roof.CenterX - fireShift;
            float distanceToHamsterCenter = Math.Abs(hamster.CenterX - shiftedRoofCenterX);
            return distanceToHamsterCenter <= GetRoofSupportRadius(roof);
        }

        /// <summary>
        /// Возвращает radius поиска roof support, повторяя HelpMethods.FindBigNotAliveUnderHamster.
        /// </summary>
        private static float GetRoofSupportRadius(ObstacleSnapshot roof)
        {
            float roofWidth = roof.RightX - roof.LeftX;
            return roofWidth * 0.5f + Consts.BigNotAliveEdgeTolerance;
        }
    }
}
