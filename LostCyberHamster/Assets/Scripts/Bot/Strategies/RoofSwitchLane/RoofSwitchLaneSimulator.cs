using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Симулирует переход после смены линии с текущей крыши на крышу другой линии.
    /// </summary>
    internal sealed class RoofSwitchLaneSimulator : ISimulator
    {
        /// <summary>
        /// Возвращает тип action, который обслуживает simulator.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.RoofSwitchLane;

        /// <summary>
        /// Строит planning-состояние после завершенного roof switch-lane.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет совместимость входа.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Применяет смену линии.
            HamsterSnapshot nextHamster = ApplyCompletedRoofSwitchLane(planningState.Hamster, action);

            // Обрабатывает immediate collectible.
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

            // Продвигает planning state после смены линии.
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
            // Проверяет совместимость входа.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Применяет ожидаемый результат смены линии.
            HamsterSnapshot nextHamster = ApplyCompletedRoofSwitchLane(planningState.Hamster, action);

            // Применяет уже достигнутую collectible-ценность.
            if (action.FulfillsCollectibleObjective)
            {
                nextHamster = CollectibleValuePolicy.ApplyValue(
                    nextHamster,
                    action.CollectibleObjectiveValue);
            }

            // Выбирает режим обновления snapshot-а.
            InProgressProjectionOptions projectionOptions = action.FulfillsCollectibleObjective
                ? InProgressProjectionOptions.RemoveObstacleAndRescan(action.TargetObstacleInstanceId.Value)
                : InProgressProjectionOptions.RescanFromStart();

            // Проецирует состояние до завершения action.
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                projectionOptions,
                remainingPostFireWorldShift: remainingPostFireWorldShift);
        }

        private static HamsterSnapshot ApplyCompletedRoofSwitchLane(
            HamsterSnapshot hamster,
            PlannedAction action)
        {
            HamsterSnapshot switchedHamster = PlanningStateTransition.ApplyLaneSwitch(hamster, action);
            if (switchedHamster == null
                || action.ResultRoofSupportInstanceId.HasValue
                || switchedHamster.HamsterState != HamsterStateEnum.RunFromRoof)
            {
                return switchedHamster;
            }

            return new HamsterSnapshot(
                HamsterStateEnum.Run,
                switchedHamster.IsOnBottomLine,
                isOnRoof: false,
                switchedHamster.Energy,
                switchedHamster.Lives,
                isShifting: false,
                roofSupportInstanceId: null,
                switchedHamster.HamsterLeftX,
                switchedHamster.HamsterRightX,
                switchedHamster.HamsterBottomY,
                switchedHamster.HamsterTopY);
        }
    }
}
