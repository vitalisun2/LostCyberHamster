using System.Collections.Generic;

namespace Assets.Scripts.Bot.Strategies.Shared.FireWindows
{
    internal delegate bool FireMomentCheck(float fireMoment);

    /// <summary>
    /// Сканирует fire window и находит участки, где action успешно проходит проверку.
    /// </summary>
    internal static class FireWindowScanner
    {
        public static List<FireInterval> FindSuccessfulIntervals(
            FireWindow window,
            float step,
            float epsilon,
            FireMomentCheck isFireSuccessful)
        {
            var intervals = new List<FireInterval>();
            if (step <= 0f || isFireSuccessful == null)
                return intervals;

            bool isInsideInterval = false;
            float intervalStart = 0f;
            float previousFireMoment = window.FirstFireShift;

            for (float candidateFireMoment = window.FirstFireShift;
                  candidateFireMoment <= window.LastFireShift + epsilon;
                  candidateFireMoment += step)
            {
                float fireMoment = candidateFireMoment > window.LastFireShift
                    ? window.LastFireShift
                    : candidateFireMoment;

                if (isFireSuccessful(fireMoment))
                {
                    if (!isInsideInterval)
                    {
                        intervalStart = fireMoment;
                        isInsideInterval = true;
                    }
                }
                else if (isInsideInterval)
                {
                    intervals.Add(new FireInterval(intervalStart, previousFireMoment));
                    isInsideInterval = false;
                }

                previousFireMoment = fireMoment;
                if (fireMoment >= window.LastFireShift)
                    break;
            }

            if (isInsideInterval)
                intervals.Add(new FireInterval(intervalStart, previousFireMoment));

            return intervals;
        }
    }
}
