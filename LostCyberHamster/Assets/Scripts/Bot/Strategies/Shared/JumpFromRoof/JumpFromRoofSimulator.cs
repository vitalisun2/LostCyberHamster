using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof
{
    /// <summary>
    /// Симулирует planning-переход после успешного прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class JumpFromRoofSimulator : ISimulator
    {
        private readonly IJumpFromRoofPolicy _policy;

        /// <summary>
        /// Создает simulator для конкретного варианта прыжка с крыши.
        /// </summary>
        public JumpFromRoofSimulator(IJumpFromRoofPolicy policy)
        {
            _policy = policy;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Симулирует завершенный прыжок с крыши с переходом в Run.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(
                planningState.Hamster,
                action);

            return PlanningStateTransition.Advance(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        /// <summary>
        /// Проецирует незавершенный прыжок с крыши в planning-state.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(
                planningState.Hamster,
                action);

            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }
    }
}
