namespace Assets.Scripts.GameEngine.Controllers
{
    /// <summary>
    /// Управляет коротким gameplay-time импульсом камеры.
    /// </summary>
    public interface ICameraShake
    {
        void Play(float multiplier);
        void Tick(float deltaTime);
        void SetPaused(bool isPaused);
        void Stop();
    }
}
