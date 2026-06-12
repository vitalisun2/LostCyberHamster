using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOver
{
    /// <summary>
    /// Симулирует planning-переход после успешного ground jump-over.
    /// </summary>
    internal sealed class JumpOverSimulator : ISimulator
    {
        private readonly IJumpOverPolicy _policy;

        public JumpOverSimulator(IJumpOverPolicy policy)
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

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false,
                remainingPostFireWorldShift: remainingPostFireWorldShift);
        }
    }
}
