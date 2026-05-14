using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnRoof
{
    /// <summary>
    /// Проверяет применимость действия запрыгивания на крышу.
    /// </summary>
    internal sealed class JumpOnRoofSpecification
    {
        private readonly IJumpOnRoofPolicy _policy;

        public JumpOnRoofSpecification(IJumpOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Проверяет, может ли hamster сейчас выполнить действие запрыгивания на крышу.
        /// </summary>
        public bool IsSatisfiedBy(PlanningState planningState)
        {
            if (planningState == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            return !hamster.IsOnRoof
                   && !hamster.IsShifting
                   && hamster.Energy >= _policy.EnergyCost;
        }
    }
}
