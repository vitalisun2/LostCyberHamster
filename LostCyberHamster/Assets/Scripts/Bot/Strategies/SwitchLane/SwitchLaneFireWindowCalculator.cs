using System.Collections.Generic;
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
        /// Собирает representative fire shifts из безопасных интервалов смены линии.
        /// </summary>
        public IReadOnlyList<float> CollectFireShifts(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            // Собирает все безопасные интервалы запуска.
            List<SafeInterval> safeIntervals = CollectSafeFireIntervals(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift);

            // Выбирает по одной точке внутри каждого безопасного интервала.
            var fireShifts = new List<float>(safeIntervals.Count);
            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval interval = safeIntervals[intervalIndex];
                if (interval.TrySelectInteriorPoint(
                        lateBudget: 0f,
                        SwitchLaneTiming.InteriorSelectionRatio,
                        out float fireShift,
                        epsilon: 0f))
                {
                    fireShifts.Add(fireShift);
                }
            }

            return fireShifts;
        }

        /// <summary>
        /// Строит безопасные интервалы запуска смены линии до заданной верхней границы.
        /// </summary>
        public List<SafeInterval> CollectSafeFireIntervals(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
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

            return safeIntervals;
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
                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                float overlapStart = obstacle.LeftX - hamster.HamsterRightX;
                float overlapEnd = obstacle.RightX - hamster.HamsterLeftX;
                float unsafeStart = overlapStart - SwitchLaneTiming.DecisionTravel;
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
    }
}
