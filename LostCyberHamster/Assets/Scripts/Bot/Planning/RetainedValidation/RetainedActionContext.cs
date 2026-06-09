using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.RetainedValidation
{
    /// <summary>
    /// Хранит role-based context проверки сохраненного action.
    /// </summary>
    internal sealed class RetainedActionContext
    {
        /// <summary>
        /// Создает context проверки retained-action для новой strategy.
        /// </summary>
        public RetainedActionContext(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot retainedObstacle,
            int retainedObstacleIndex,
            PlannedAction action)
        {
            PlanningState = planningState;
            ProjectedWorldSnapshot = projectedWorldSnapshot;
            RetainedObstacle = retainedObstacle;
            RetainedObstacleIndex = retainedObstacleIndex;
            Action = action;
        }

        public PlanningState PlanningState { get; }
        public WorldSnapshot ProjectedWorldSnapshot { get; }
        public ObstacleSnapshot RetainedObstacle { get; }
        public int RetainedObstacleIndex { get; }
        public PlannedAction Action { get; }
    }
}
