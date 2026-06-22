using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.Contracts
{
    /// <summary>
    /// Описывает predicate-спецификацию стратегии для уже выбранного subject.
    /// </summary>
    internal interface IActionSubjectSpecification
    {
        /// <summary>
        /// Возвращает true, если стратегия применима к указанному subject в текущем planning state.
        /// </summary>
        bool IsSubjectValid(
            PlanningState planningState,
            ObstacleSnapshot obstacle);
    }
}
