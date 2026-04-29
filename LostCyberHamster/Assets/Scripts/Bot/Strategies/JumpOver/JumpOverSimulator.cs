using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Симулирует planning-переход обычного jump-over.
    /// </summary>
    internal sealed class JumpOverSimulator : ISimulator
    {
        /// <summary>
        /// Тип действия, которое умеет симулировать этот simulator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOver;

        /// <summary>
        /// Строит planning state после полностью выполненного jump-over.
        /// </summary>
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

        /// <summary>
        /// Строит planning state для action, который уже был fired, но ещё не завершился.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }
    }
}
