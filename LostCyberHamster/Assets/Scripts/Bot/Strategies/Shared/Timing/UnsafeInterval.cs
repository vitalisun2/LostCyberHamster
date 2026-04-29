namespace Assets.Scripts.Bot.Strategies.Shared.Timing
{
    internal readonly struct UnsafeInterval
    {
        public UnsafeInterval(float start, float end)
        {
            Start = start;
            End = end;
        }

        public float Start { get; }
        public float End { get; }
    }
}
