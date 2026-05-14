using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Хранит границы roof-hazard chain и выбранный fire shift для roof jump-over.
    /// </summary>
    internal readonly struct RoofJumpOverChainModel
    {
        public RoofJumpOverChainModel(
            ObstacleSnapshot firstHazard,
            int firstHazardIndex,
            ObstacleSnapshot lastHazard,
            int lastHazardIndex,
            int hazardCount,
            float firstFireShift,
            float lastFireShift,
            float selectedFireShift)
        {
            FirstHazard = firstHazard;
            FirstHazardIndex = firstHazardIndex;
            LastHazard = lastHazard;
            LastHazardIndex = lastHazardIndex;
            HazardCount = hazardCount;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
            SelectedFireShift = selectedFireShift;
        }

        public ObstacleSnapshot FirstHazard { get; }
        public int FirstHazardIndex { get; }
        public ObstacleSnapshot LastHazard { get; }
        public int LastHazardIndex { get; }
        public int HazardCount { get; }
        public float FirstFireShift { get; }
        public float LastFireShift { get; }
        public float SelectedFireShift { get; }
    }
}