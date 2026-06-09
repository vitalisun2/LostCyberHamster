using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Симулирует planning-переход после прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofSimulator : ISimulator
    {
        /// <summary>
        /// Policy конкретного варианта roof-to-roof прыжка.
        /// </summary>
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        public JumpFromRoofOnRoofSimulator(IJumpFromRoofOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Тип action, который симулирует simulator.
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
            // Проверяет, что action подходит этому simulator.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Применяет итоговое roof-run состояние.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
                planningState.Hamster,
                action);

            // Продвигает planning state после завершения action.
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
            // Проверяет, что action подходит этому simulator.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Проецирует ожидаемое состояние после in-progress action.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
                planningState.Hamster,
                action);

            // Строит projected planning state без повторной обработки target roof.
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: true);
        }
    }
}
