namespace Vues.GameCore
{
    /// <summary>
    /// Хранит времена одной Skateboard landing wave после cycle scaling.
    /// </summary>
    internal readonly struct SkateboardLandingImpactTimeline
    {
        public SkateboardLandingImpactTimeline(
            float waveDuration,
            float bumpDuration,
            float destroyDelay,
            float cameraShakeDurationMultiplier,
            float cameraShakeFrequencyMultiplier)
        {
            WaveDuration = waveDuration;
            BumpDuration = bumpDuration;
            DestroyDelay = destroyDelay;
            CameraShakeDurationMultiplier = cameraShakeDurationMultiplier;
            CameraShakeFrequencyMultiplier = cameraShakeFrequencyMultiplier;
        }

        public float WaveDuration { get; }
        public float BumpDuration { get; }
        public float DestroyDelay { get; }
        public float CameraShakeDurationMultiplier { get; }
        public float CameraShakeFrequencyMultiplier { get; }
    }
}
