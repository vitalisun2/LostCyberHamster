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
        public bool TryGetLatestFireShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            out float latestFireShift)
        {
            latestFireShift = targetObstacle.LeftX
                - hamster.HamsterRightX
                - SwitchLaneTiming.ExecutionLeadDistance;
            return latestFireShift > 0f;
        }

        public IReadOnlyList<float> CollectFireShifts(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            List<SafeInterval> safeIntervals = CollectSafeFireIntervals(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift);

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

        public List<SafeInterval> CollectSafeFireIntervals(
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

                float safeEnd = interval.Start;
                if (safeEnd >= safeStart)
                    safeIntervals.Add(new SafeInterval(safeStart, safeEnd));

                if (interval.End > safeStart)
                    safeStart = interval.End;
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

            return unsafeIntervals;
        }
    }
}
