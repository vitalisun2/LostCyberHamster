namespace Assets.Scripts.Bot.Planning.Strategies
{
    internal readonly struct SafeInterval
    {
        public SafeInterval(float start, float end)
        {
            Start = start;
            End = end;
        }

        public float Start { get; }
        public float End { get; }
    }
}
