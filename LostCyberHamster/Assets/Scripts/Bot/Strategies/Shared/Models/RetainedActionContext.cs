using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.Models
{
    /// <summary>
    /// Хранит общий контекст проверки сохранённого action.
    /// </summary>
    internal sealed class RetainedActionContext
    {
        public RetainedActionContext(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            PlannedAction action)
        {
            PlanningState = planningState;
            ProjectedWorldSnapshot = projectedWorldSnapshot;
            DecisionPoint = decisionPoint;
            TargetObstacle = targetObstacle;
            TargetObstacleIndex = targetObstacleIndex;
            Action = action;
        }

        public PlanningState PlanningState { get; }
        public WorldSnapshot ProjectedWorldSnapshot { get; }
        public DecisionPoint DecisionPoint { get; }
        public ObstacleSnapshot TargetObstacle { get; }
        public int TargetObstacleIndex { get; }
        public PlannedAction Action { get; }
    }
}
