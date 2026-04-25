using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.Interfaces
{
    /// <summary>
    /// Симулирует planning-переходы для действия strategy.
    /// </summary>
    internal interface ISimulator
    {
        BotActionKind ActionKind { get; }

        PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot);

        PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot);
    }
}
