using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.PassiveCollect
{
    /// <summary>
    /// Симулирует planning-переход после passive collectable pickup.
    /// </summary>
    internal sealed class PassiveCollectSimulator : ISimulator
    {
        public BotActionKind ActionKind => BotActionKind.PassiveCollect;

        /// <summary>
        /// Строит planning-состояние после подбора collectable и применения его value.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = CollectibleValuePolicy.ApplyValue(
                planningState.Hamster,
                action.CollectibleObjectiveValue);
            return PlanningStateTransition.AdvanceAfterCollectiblePickup(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        /// <summary>
        /// Проецирует уже начатый passive collect до ожидаемого pickup.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = CollectibleValuePolicy.ApplyValue(
                planningState.Hamster,
                action.CollectibleObjectiveValue);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false,
                remainingPostFireWorldShift: remainingPostFireWorldShift,
                startObstacleIndexOverride: 0,
                removedObstacleInstanceIdAfterCompletion: action.TargetObstacleInstanceId);
        }
    }
}
