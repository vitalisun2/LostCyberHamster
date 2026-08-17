using Assets.Scripts.Gameplay;

namespace Vues.GameCore
{
    /// <summary>
    /// Хранит неизменяемый вход одной Skateboard landing wave.
    /// </summary>
    internal readonly struct SkateboardLandingImpactRequest
    {
        public SkateboardLandingImpactRequest(
            bool isSuperCycle,
            bool startedOnRoof,
            Obstacle currentSupport)
        {
            IsSuperCycle = isSuperCycle;
            StartedOnRoof = startedOnRoof;
            CurrentSupport = currentSupport;
        }

        public bool IsSuperCycle { get; }
        public bool StartedOnRoof { get; }
        public Obstacle CurrentSupport { get; }
    }
}
