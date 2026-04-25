using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Strategies.Shared.Interfaces
{
    /// <summary>
    /// Проверяет, можно ли сохранить committed-действие конкретного strategy.
    /// </summary>
    internal interface IRetainedActionValidator
    {
        BotActionKind ActionKind { get; }

        bool IsStillValid(RetainedActionContext context);
    }
}
