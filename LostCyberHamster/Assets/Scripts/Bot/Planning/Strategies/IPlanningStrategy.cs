using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    public interface IPlanningStrategy
    {
        bool TryGenerate(
            PlanningState planningState,
            BotPerceptionSnapshot perceptionSnapshot,
            VisibleObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            out PlannedAction action);
    }
}
