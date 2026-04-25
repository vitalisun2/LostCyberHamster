namespace Assets.Scripts.Bot.Strategies.Shared.Models
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
