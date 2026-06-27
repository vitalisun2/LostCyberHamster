using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Хранит рассчитанный candidate смены линии с текущей крыши на крышу другой линии.
    /// </summary>
    internal readonly struct RoofSwitchLaneModel
    {
        public RoofSwitchLaneModel(
            ObstacleSnapshot contextObstacle,
            int contextObstacleIndex,
            ObstacleSnapshot targetRoof,
            int targetRoofIndex,
            bool targetBottomLine,
            SwitchLaneFireWindowSample fireWindowSample,
            CollectibleObjectiveValue objectiveValue)
        {
            ContextObstacle = contextObstacle;
            ContextObstacleIndex = contextObstacleIndex;
            TargetRoof = targetRoof;
            TargetRoofIndex = targetRoofIndex;
            TargetBottomLine = targetBottomLine;
            FireWindowSample = fireWindowSample;
            ObjectiveValue = objectiveValue;
        }

        /// <summary>
        /// Obstacle, по которому рассчитывается deadline запуска.
        /// </summary>
        public ObstacleSnapshot ContextObstacle { get; }

        /// <summary>
        /// World-index context obstacle в исходном snapshot.
        /// </summary>
        public int ContextObstacleIndex { get; }

        /// <summary>
        /// Roof support на целевой линии после смены линии.
        /// </summary>
        public ObstacleSnapshot TargetRoof { get; }

        /// <summary>
        /// World-index target roof в исходном snapshot.
        /// </summary>
        public int TargetRoofIndex { get; }

        /// <summary>
        /// Целевая линия после смены линии.
        /// </summary>
        public bool TargetBottomLine { get; }

        /// <summary>
        /// Выбранное окно запуска switch-lane.
        /// </summary>
        public SwitchLaneFireWindowSample FireWindowSample { get; }

        /// <summary>
        /// Ценность collectable, если switch сам успевает подобрать context collectable.
        /// </summary>
        public CollectibleObjectiveValue ObjectiveValue { get; }

        public float FireShift => FireWindowSample.FireShift;

        public float CompletionWorldShift => FireShift + SwitchLaneTiming.DecisionTravel;
    }
}
