using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning.Strategies
{
    public interface IPlanningStrategy
    {
        BotActionKind ActionKind { get; }

        bool TryGenerate(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out PlannedAction action);

        PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot);
    }
}
