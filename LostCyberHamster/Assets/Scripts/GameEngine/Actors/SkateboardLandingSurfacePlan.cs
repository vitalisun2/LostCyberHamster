using Assets.Scripts.Gameplay;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Хранит предсказанную roof support одного Skateboard jump-cycle.
    /// </summary>
    internal readonly struct SkateboardLandingSurfacePlan
    {
        public SkateboardLandingSurfacePlan(Obstacle support)
        {
            Support = support;
        }

        public Obstacle Support { get; }
    }
}
