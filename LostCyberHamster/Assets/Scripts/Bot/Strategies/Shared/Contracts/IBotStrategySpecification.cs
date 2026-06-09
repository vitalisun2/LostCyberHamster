using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.Contracts
{
    /// <summary>
    /// Описывает predicate-спецификацию стратегии для уже выбранного role-based obstacle.
    /// </summary>
    internal interface IBotStrategySpecification
    {
        /// <summary>
        /// Возвращает true, если стратегия применима к указанному obstacle в текущем planning state.
        /// </summary>
        bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle);
    }
}
