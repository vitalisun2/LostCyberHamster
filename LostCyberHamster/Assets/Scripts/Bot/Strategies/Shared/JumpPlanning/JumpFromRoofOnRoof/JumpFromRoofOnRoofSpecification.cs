using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof
{
    /// <summary>
    /// Проверяет применимость прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofSpecification
    {
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        public JumpFromRoofOnRoofSpecification(IJumpFromRoofOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Проверяет, может ли hamster сейчас выполнить прыжок с крыши на следующую крышу.
        /// </summary>
        public bool IsSatisfiedBy(PlanningState planningState)
        {
            if (planningState == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.RoofSupportInstanceId.HasValue
                && !hamster.IsShifting
                && hamster.Energy >= _policy.EnergyCost;
        }
    }
}
