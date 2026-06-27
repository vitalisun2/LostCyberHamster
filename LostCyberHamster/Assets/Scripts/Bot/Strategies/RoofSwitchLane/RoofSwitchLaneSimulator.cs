using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Симулирует переход после смены линии с текущей крыши на крышу другой линии.
    /// </summary>
    internal sealed class RoofSwitchLaneSimulator : ISimulator
    {
        public BotActionKind ActionKind => BotActionKind.RoofSwitchLane;

        /// <summary>
        /// Строит planning-состояние после завершенного roof switch-lane.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyLaneSwitch(planningState.Hamster, action);
            if (action.FulfillsCollectibleObjective)
            {
                nextHamster = CollectibleValuePolicy.ApplyValue(
                    nextHamster,
                    action.CollectibleObjectiveValue);

                return PlanningStateTransition.AdvanceAfterCollectiblePickup(
                    planningState,
                    action,
                    worldSnapshot,
                    nextHamster);
            }

            return PlanningStateTransition.AdvanceAfterLaneSwitch(
                planningState,
                action,
                worldSnapshot,
                nextHamster);
        }

        /// <summary>
        /// Проецирует уже запущенный roof switch-lane до ожидаемого завершения.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot,
            float? remainingPostFireWorldShift = null)
        {
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            HamsterSnapshot nextHamster = PlanningStateTransition.ApplyLaneSwitch(planningState.Hamster, action);
            if (action.FulfillsCollectibleObjective)
            {
                nextHamster = CollectibleValuePolicy.ApplyValue(
                    nextHamster,
                    action.CollectibleObjectiveValue);
            }

            InProgressProjectionOptions projectionOptions = action.FulfillsCollectibleObjective
                ? InProgressProjectionOptions.RemoveObstacleAndRescan(action.TargetObstacleInstanceId.Value)
                : InProgressProjectionOptions.RescanFromStart();

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
