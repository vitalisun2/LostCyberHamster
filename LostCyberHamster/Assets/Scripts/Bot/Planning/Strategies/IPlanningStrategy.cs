using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    public interface IPlanningStrategy
    {
        bool TryGenerate(
            PlanningState planningState,
            WorldSnapshot perceptionSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            out PlannedAction action);
    }
}
