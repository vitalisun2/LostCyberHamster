using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Симулирует planning-переходы смены линии.
    /// </summary>
    internal sealed class SwitchLaneSimulator : ISimulator
    {
        public BotActionKind ActionKind => BotActionKind.SwitchLane;

        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyLaneSwitch(planningState.Hamster, action);
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot hamster = planningState.Hamster;
            HamsterSnapshot nextHamster = new(
                hamster.HamsterState,
                hamster.IsOnBottomLine,
                isOnRoof: false,
                hamster.Energy,
                hamster.Lives,
                hamster.IsDamaged,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX);

            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }
    }
}
