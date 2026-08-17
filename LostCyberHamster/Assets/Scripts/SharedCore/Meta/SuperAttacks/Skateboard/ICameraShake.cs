namespace Vues.GameCore
{
    /// <summary>
    /// Управляет коротким gameplay-time импульсом камеры.
    /// </summary>
    public interface ICameraShake
    {
        void Play(
            float amplitudeMultiplier,
            float durationMultiplier,
            float frequencyMultiplier);
        void Tick(float deltaTime);
        void SetPaused(bool isPaused);
        void Stop();
    }
}
