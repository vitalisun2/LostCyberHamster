namespace Assets.Scripts.Bot.Strategies.Shared.FireWindows
{
    /// <summary>
    /// Хранит весь диапазон, где action теоретически можно выполнить.
    /// </summary>
    internal readonly struct FireWindow
    {
        public FireWindow(float firstFireShift, float lastFireShift)
        {
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
        }

        public float FirstFireShift { get; }
        public float LastFireShift { get; }
    }
}
