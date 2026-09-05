using System;

namespace Assets.Scripts.Online
{
    /// <summary>Предоставляет календарное время и независимое от timeScale время ожидания.</summary>
    public interface IGameClock
    {
        DateTime LocalNow { get; }
        DateTime UtcNow { get; }
        double RealtimeSeconds { get; }
    }
}
