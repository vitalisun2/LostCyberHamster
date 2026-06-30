using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Хранит target context для defensive или reward roof switch-lane сценария.
    /// </summary>
    internal readonly struct RoofSwitchLaneTarget
    {
        public RoofSwitchLaneTarget(
            ObstacleSnapshot contextObstacle,
            int contextObstacleIndex,
            bool targetBottomLine,
            CollectibleObjectiveValue objectiveValue)
        {
            ContextObstacle = contextObstacle;
            ContextObstacleIndex = contextObstacleIndex;
            TargetBottomLine = targetBottomLine;
            ObjectiveValue = objectiveValue;
        }

        /// <summary>
        /// Возвращает obstacle, по которому рассчитывается deadline запуска.
        /// </summary>
        public ObstacleSnapshot ContextObstacle { get; }

        /// <summary>
        /// Возвращает world-index context obstacle в исходном snapshot.
        /// </summary>
        public int ContextObstacleIndex { get; }

        /// <summary>
        /// Возвращает целевую линию после смены линии.
        /// </summary>
        public bool TargetBottomLine { get; }

        /// <summary>
        /// Возвращает ценность collectable для reward route.
        /// </summary>
        public CollectibleObjectiveValue ObjectiveValue { get; }
    }
}
