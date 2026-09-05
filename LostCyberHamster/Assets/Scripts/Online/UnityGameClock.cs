using System;
using UnityEngine;

namespace Assets.Scripts.Online
{
    /// <summary>Общий источник времени устройства для локальной игры.</summary>
    public sealed class UnityGameClock : IGameClock
    {
        public static IGameClock Instance { get; } = new UnityGameClock();
        public DateTime LocalNow => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
        public double RealtimeSeconds => Time.realtimeSinceStartupAsDouble;
    }
}
