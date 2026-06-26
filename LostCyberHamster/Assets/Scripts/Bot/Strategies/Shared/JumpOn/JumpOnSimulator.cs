using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Симулирует planning-переход после успешного ground jump-on.
    /// </summary>
    internal sealed class JumpOnSimulator : ISimulator
    {
        private readonly IJumpOnPolicy _policy;

        /// <summary>
        /// Создает simulator для конкретного jump-on policy.
        /// </summary>
        public JumpOnSimulator(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Строит planning-состояние после завершённого jump-on и удаления target obstacle.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет входные данные.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Применяет завершённый переход.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return PlanningStateTransition.AdvanceAfterTargetRemoval(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        /// <summary>
        /// Проецирует planning-состояние для action, который уже был запущен, но ещё не завершён.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            // Проверяет входные данные.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            InProgressProjectionOptions projectionOptions = action.TargetObstacleInstanceId.HasValue
                ? InProgressProjectionOptions.RemoveObstacleAndRescan(action.TargetObstacleInstanceId.Value)
                : InProgressProjectionOptions.SkipResolvedActionTarget();

            // Строит projection до ожидаемого завершения.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                projectionOptions,
                remainingPostFireWorldShift: remainingPostFireWorldShift);
        }
    }
}
