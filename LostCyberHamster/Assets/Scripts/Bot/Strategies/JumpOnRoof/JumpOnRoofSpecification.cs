using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Проверяет применимость прыжка на крышу.
    /// </summary>
    internal sealed class JumpOnRoofSpecification
    {
        public const int EnergyCost = 10;

        /// <summary>
        /// Проверяет, может ли hamster сейчас выполнить обычный jump-on-roof.
        /// </summary>
        public bool IsSatisfiedBy(PlanningState planningState)
        {
            if (planningState == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            return !hamster.IsOnRoof
                   && !hamster.IsShifting
                   && !hamster.IsDamaged
                   && hamster.Energy >= EnergyCost;
        }
    }
}
