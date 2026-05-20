using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof
{
    /// <summary>
    /// Симулирует planning-переход после прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofSimulator : ISimulator
    {
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        public JumpFromRoofOnRoofSimulator(IJumpFromRoofOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Тип действия, который обрабатывает simulator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Симулирует завершенный roof-to-roof прыжок с продолжением RoofRun.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет вход и тип действия.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Строит состояние хомяка на новой roof support.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
                planningState.Hamster,
                action);

            // Продвигает planning-state за target roof.
            return PlanningStateTransition.AdvanceAfterRoofJumpOver(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        /// <summary>
        /// Проецирует незавершенный roof-to-roof прыжок в planning-state.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет вход и тип действия.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Строит ожидаемое состояние хомяка после landing.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
                planningState.Hamster,
                action);

            // Проецирует состояние действия в процессе.
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: true);
        }
    }
}
