using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Симулирует planning-переход в RoofRun.
    /// </summary>
    internal sealed class JumpOnRoofSimulator : ISimulator
    {
        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;

        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterLanding(
                planningState.Hamster,
                action);

            return PlanningStateTransition.AdvanceAfterRoofLanding(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterLanding(
                planningState.Hamster,
                action);

            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: true);
        }
    }
}
