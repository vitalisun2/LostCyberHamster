using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Проверяет применимость super jump on roof.
    /// </summary>
    internal sealed class SuperJumpOnRoofSpecification
    {
        public const int EnergyCost = 20;

        /// <summary>
        /// Проверяет, может ли hamster сейчас выполнить super jump-on-roof.
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