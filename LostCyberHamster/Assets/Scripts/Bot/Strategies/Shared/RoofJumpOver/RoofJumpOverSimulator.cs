using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Симулирует planning-переход roof jump-over с продолжением RoofRun.
    /// </summary>
    internal sealed class RoofJumpOverSimulator : ISimulator
    {
        /// <summary>
        /// Policy конкретного варианта roof jump-over.
        /// </summary>
        private readonly IRoofJumpOverPolicy _policy;

        /// <summary>
        /// Создает simulator для конкретного варианта roof jump-over.
        /// </summary>
        public RoofJumpOverSimulator(IRoofJumpOverPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Тип action, который симулирует simulator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Симулирует завершенный roof jump-over.
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
        /// Проецирует незавершенный roof jump-over в planning-state.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            // Проверяет, что action подходит этому simulator.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Проецирует ожидаемое состояние после in-progress action.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRoofRunAfterRoofJumpOver(
                planningState.Hamster,
                action);

            // Строит projected planning state без повторной обработки target obstacle.
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
