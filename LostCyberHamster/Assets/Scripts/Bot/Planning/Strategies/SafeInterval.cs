namespace Assets.Scripts.Bot.Planning.Strategies
{
    internal readonly struct SafeInterval
    {
        private const float DefaultEpsilon = 0.0001f;

        public SafeInterval(float start, float end)
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
            float epsilon = DefaultEpsilon)
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
