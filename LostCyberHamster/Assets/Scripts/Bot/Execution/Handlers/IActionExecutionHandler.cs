using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot.Execution.Handlers
{
    internal interface IActionExecutionHandler
    {
        ActionFireResult TryFire(Hamster hamster, PlannedAction action);

        bool IsCompleted(Hamster hamster, PlannedAction action);
    }
}