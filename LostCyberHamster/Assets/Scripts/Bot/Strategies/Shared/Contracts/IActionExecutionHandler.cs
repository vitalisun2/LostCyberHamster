using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot.Strategies.Shared.Contracts
{
    internal interface IActionExecutionHandler
    {
        ActionFireResult TryFire(Hamster hamster, PlannedAction action);

        bool IsCompleted(Hamster hamster, PlannedAction action);
    }
}