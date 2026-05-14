using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Симулирует planning-переход roof jump-over с продолжением RoofRun.
    /// </summary>
    internal sealed class RoofJumpOverSimulator : ISimulator
    {
        private readonly IRoofJumpOverPolicy _policy;

        public RoofJumpOverSimulator(IRoofJumpOverPolicy policy)
        {
            _policy = policy;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
                planningState.Hamster,
                action);

            return PlanningStateTransition.AdvanceAfterRoofJumpOver(
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

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
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