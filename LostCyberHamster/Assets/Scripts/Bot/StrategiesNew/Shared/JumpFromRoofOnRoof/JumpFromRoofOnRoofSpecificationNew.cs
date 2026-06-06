using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Проверяет применимость прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofSpecificationNew
    {
        /// <summary>
        /// Policy конкретного варианта roof-to-roof прыжка.
        /// </summary>
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        public JumpFromRoofOnRoofSpecificationNew(IJumpFromRoofOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает true, если хомяк сейчас может выполнить roof-to-roof прыжок.
        /// </summary>
        public bool IsSatisfiedBy(PlanningState planningState)
        {
            // Проверяет planning state.
            if (planningState == null)
                return false;

            // Проверяет roof-run состояние и ресурс action.
            HamsterSnapshot hamster = planningState.Hamster;
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.RoofSupportInstanceId.HasValue
                && !hamster.IsShifting
                && hamster.Energy >= _policy.EnergyCost;
        }
    }
}
