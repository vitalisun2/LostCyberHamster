using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Симулирует переходы planning-состояния для действий смены линии и их незавершённой head-фазы.
    /// </summary>
    internal sealed class SwitchLaneSimulator : ISimulator
    {
        /// <summary>
        /// Возвращает тип действия, для которого симулятор рассчитывает planning-переходы.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SwitchLane;

        /// <summary>
        /// Строит следующее planning-состояние после полного завершения действия смены линии.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Отсекает неподходящий action или неполный planning context.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Применяет смену линии к snapshot хомяка.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyLaneSwitch(planningState.Hamster, action);

            // Продвигает planning-состояние до конца действия.
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        /// <summary>
        /// Строит projected planning-состояние для уже запущенного действия смены линии, которое ещё не завершилось.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Отсекает неподходящий action или неполный planning context.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Формирует состояние хомяка после фактического переключения линии.
            HamsterSnapshot hamster = planningState.Hamster;
            bool targetBottomLine = action.TargetBottomLine ?? hamster.IsOnBottomLine;
            HamsterSnapshot nextHamster = new(
                hamster.HamsterState,
                targetBottomLine,
                isOnRoof: false,
                hamster.Energy,
                hamster.Lives,
                hamster.IsDamaged,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);

            // Проецирует остаток head-action до ближайшего planning boundary.
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }
    }
}
