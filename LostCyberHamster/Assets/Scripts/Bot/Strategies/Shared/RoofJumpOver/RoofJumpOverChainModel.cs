using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Хранит границы roof-hazard chain и выбранный fire shift.
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

        /// <summary>
        /// Первый roof occupant hazard, покрываемый прыжком.
        /// </summary>
        public ObstacleSnapshot FirstHazard { get; }

        /// <summary>
        /// Индекс первого hazard в world snapshot.
        /// </summary>
        public int FirstHazardIndex { get; }

        /// <summary>
        /// Последний roof occupant hazard, покрываемый прыжком.
        /// </summary>
        public ObstacleSnapshot LastHazard { get; }

        /// <summary>
        /// Индекс последнего hazard в world snapshot.
        /// </summary>
        public int LastHazardIndex { get; }

        /// <summary>
        /// Количество hazards, покрытых одним прыжком.
        /// </summary>
        public int HazardCount { get; }

        /// <summary>
        /// Левая граница fire-window.
        /// </summary>
        public float FirstFireShift { get; }

        /// <summary>
        /// Правая граница fire-window.
        /// </summary>
        public float LastFireShift { get; }

        /// <summary>
        /// Выбранный fire shift внутри окна.
        /// </summary>
        public float SelectedFireShift { get; }
    }
}
