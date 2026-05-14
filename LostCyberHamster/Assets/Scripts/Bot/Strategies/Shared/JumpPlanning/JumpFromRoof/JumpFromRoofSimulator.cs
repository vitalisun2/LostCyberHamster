using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Симулирует planning-переход после успешного прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class JumpFromRoofSimulator : ISimulator
    {
        /// <summary>
        /// Хранит runtime-отличия конкретного варианта прыжка с крыши.
        /// </summary>
        private readonly IJumpFromRoofPolicy _policy;

        public JumpFromRoofSimulator(IJumpFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Тип действия, который обрабатывает simulator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Симулирует завершенный прыжок с крыши с переходом в Run.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет вход и тип действия.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Строит состояние хомяка после успешного схода на дорогу.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);

            // Продвигает planning-state после завершения действия.
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        /// <summary>
        /// Проецирует незавершенный прыжок с крыши в planning-state.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет вход и тип действия.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Строит состояние хомяка после ожидаемого завершения.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);

            // Проецирует состояние действия в процессе.
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }
    }
}
