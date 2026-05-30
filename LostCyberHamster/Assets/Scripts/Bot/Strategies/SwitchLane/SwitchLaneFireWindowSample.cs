namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Хранит выбранный fire shift вместе с безопасным интервалом, из которого он взят.
    /// </summary>
    internal readonly struct SwitchLaneFireWindowSample
    {
        public SwitchLaneFireWindowSample(float fireShift, float firstFireShift, float lastFireShift)
        {
            FireShift = fireShift;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
        }

        public float FireShift { get; }
        public float FirstFireShift { get; }
        public float LastFireShift { get; }
    }
}
