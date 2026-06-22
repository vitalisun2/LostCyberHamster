using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.PassiveAdvance
{
    /// <summary>
    /// Симулирует no-input продвижение мира без изменения состояния хомяка.
    /// </summary>
    internal sealed class PassiveAdvanceSimulator : ISimulator
    {
        public BotActionKind ActionKind => BotActionKind.PassiveAdvance;

        /// <summary>
        /// Строит planning-состояние после безопасного пассивного продвижения.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            return PlanningStateTransition.Advance(
                planningState,
                action,
                worldSnapshot,
                planningState.Hamster);
        }

        /// <summary>
        /// Проецирует уже начатое passive advance ожидание до ожидаемой границы.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                planningState.Hamster,
                skipTargetObstacleAfterCompletion: true,
                remainingPostFireWorldShift: remainingPostFireWorldShift);
        }
    }
}
