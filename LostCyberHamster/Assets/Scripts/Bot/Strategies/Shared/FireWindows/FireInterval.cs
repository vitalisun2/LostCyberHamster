namespace Assets.Scripts.Bot.Strategies.Shared.FireWindows
{
    /// <summary>
    /// Хранит участок fire window, где проверка action проходит успешно.
    /// </summary>
    internal readonly struct FireInterval
    {
        public FireInterval(float start, float end)
        {
            Start = start;
            End = end;
        }

        public float Start { get; }
        public float End { get; }

        /// <summary>
        /// Выбирает точку внутри интервала с возможным отступом от правого края.
        /// </summary>
        public bool TrySelectPoint(
            float positionInInterval,
            float distanceFromIntervalEnd,
            float epsilon,
            out float fireMoment)
        {
            float effectiveEnd = End - distanceFromIntervalEnd;
            if (effectiveEnd <= Start + epsilon)
            {
                fireMoment = 0f;
                return false;
            }

            fireMoment = Start + (effectiveEnd - Start) * positionInInterval;
            return true;
        }
    }
}
