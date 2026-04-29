using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Timing;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Сканирует fire-window и выбирает точку внутри exact-outcome интервала.
    /// </summary>
    internal static class JumpFireShiftScanner
    {
        private const float _searchStep = 0.005f;
        private const float _searchEpsilon = 0.0001f;
        private const float _interiorSelectionRatio = 0.5f;
        private const float _lateFireSafetyBudget = 0.1f;

        public static bool TrySelectFireShift(
            float firstFireShift,
            float lastFireShift,
            bool preferLatestFireShift,
            Func<float, bool> isExactOutcome,
            out float fireShift,
            out SafeInterval selectedInterval,
            out int exactIntervalCount)
        {
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

                if (isExactOutcome(clampedFireShift))
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

            exactIntervalCount = exactOutcomeIntervals.Count;
            for (int intervalIndex = exactOutcomeIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                SafeInterval interval = exactOutcomeIntervals[intervalIndex];
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
    }
}