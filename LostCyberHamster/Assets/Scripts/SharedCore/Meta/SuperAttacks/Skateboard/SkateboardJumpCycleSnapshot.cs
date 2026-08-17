using Assets.Scripts.GameEngine.Actors;

namespace Vues.GameCore
{
    /// <summary>
    /// Хранит неизменяемые origin и landing intent одного Skateboard jump-cycle.
    /// </summary>
    internal readonly struct SkateboardJumpCycleSnapshot
    {
        public SkateboardJumpCycleSnapshot(
            long actionId,
            bool startedOnRoof,
            SkateboardLandingSurfacePlan landingPlan)
        {
            ActionId = actionId;
            StartedOnRoof = startedOnRoof;
            LandingPlan = landingPlan;
        }

        public long ActionId { get; }
        public bool StartedOnRoof { get; }
        public SkateboardLandingSurfacePlan LandingPlan { get; }
    }
}
