using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof
{
    /// <summary>
    /// Симулирует planning-переход после посадки на крышу.
    /// </summary>
    internal sealed class JumpOnRoofSimulator : ISimulator
    {
        private readonly IJumpOnRoofPolicy _policy;

        /// <summary>
        /// Создает simulator для конкретного jump-on-roof policy.
        /// </summary>
        public JumpOnRoofSimulator(IJumpOnRoofPolicy policy)
        {
            _policy = policy;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает planning state после завершенной посадки на крышу.
        /// </summary>
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

        /// <summary>
        /// Проецирует состояние для действия, которое уже запущено и еще не завершилось.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
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
                skipTargetObstacleAfterCompletion: true,
                remainingPostFireWorldShift: remainingPostFireWorldShift);
        }
    }
}
