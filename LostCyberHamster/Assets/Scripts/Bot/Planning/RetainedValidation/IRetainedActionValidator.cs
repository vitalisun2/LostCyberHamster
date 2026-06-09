using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning.RetainedValidation
{
    /// <summary>
    /// Проверяет, можно ли сохранить committed-действие новой role-based strategy.
    /// </summary>
    internal interface IRetainedActionValidator
    {
        /// <summary>
        /// Возвращает тип action, который валидирует этот validator.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Возвращает true, если retained-action все еще актуален и безопасен.
        /// </summary>
        bool IsStillValid(RetainedActionContext context);
    }
}
