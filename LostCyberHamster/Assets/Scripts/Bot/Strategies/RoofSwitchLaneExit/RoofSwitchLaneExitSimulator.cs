using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Симулирует RoofRun -> switch lane -> RunFromRoof -> Run.
    /// </summary>
    internal sealed class RoofSwitchLaneExitSimulator : ISimulator
    {
        private readonly RoofSwitchLaneExitPolicy _policy;

        public RoofSwitchLaneExitSimulator(RoofSwitchLaneExitPolicy policy)
        {
            _policy = policy;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Симулирует завершенный сход с крыши через смену линии.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = ApplyRunAfterRoofSwitchLaneExit(planningState.Hamster, action);
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        /// <summary>
        /// Проецирует уже запущенный сход с крыши через смену линии.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = ApplyRunAfterRoofSwitchLaneExit(planningState.Hamster, action);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }

        /// <summary>
        /// Возвращает состояние Run после завершения схода с крыши через смену линии.
        /// </summary>
        private static HamsterSnapshot ApplyRunAfterRoofSwitchLaneExit(
            HamsterSnapshot hamster,
            PlannedAction action)
        {
            bool targetBottomLine = action.TargetBottomLine ?? hamster.IsOnBottomLine;
            return new HamsterSnapshot(
                HamsterStateEnum.Run,
                targetBottomLine,
                isOnRoof: false,
                hamster.Energy - action.EnergyCost,
                hamster.Lives,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }
    }
}
