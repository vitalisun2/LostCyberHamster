using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof
{
    /// <summary>
    /// Симулирует planning-переход после успешного roof-to-road jump-on.
    /// </summary>
    internal sealed class JumpOnFromRoofSimulator : ISimulator
    {
        /// <summary>
        /// Политика runtime-различий конкретного варианта.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        public JumpOnFromRoofSimulator(IJumpOnFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает тип action, который симулирует этот экземпляр.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Строит planning-состояние после завершённого roof-to-road jump-on и удаления target obstacle.
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
            WorldSnapshot worldSnapshot)
        {
            // Проверяет входные данные.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Строит projection до ожидаемого завершения.
            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: true);
        }
    }
}
