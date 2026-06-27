using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Планирует смену линии с текущей крыши на крышу другой линии.
    /// </summary>
    internal sealed class RoofSwitchLaneStrategy : IPlanningStrategy
    {
        private readonly RoofSwitchLanePlanner _planner;

        public RoofSwitchLaneStrategy()
        {
            var fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _planner = new RoofSwitchLanePlanner(fireWindowCalculator);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new RoofSwitchLaneExecutor(triggerGate);
            Simulator = new RoofSwitchLaneSimulator();
        }

        public BotActionKind ActionKind => BotActionKind.RoofSwitchLane;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Проверяет две причины применимости: defensive уход с текущей roof lane и reward route на opposite roof lane.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            if (!PlanningStrategyApplicability.HasContext(planningState, decisionPoint)
                || !PlanningStrategyApplicability.CanPlanRoofRun(planningState.Hamster))
            {
                return false;
            }

            if (PlanningStrategyApplicability.IsCurrentLane(planningState, decisionPoint))
            {
                return PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.BlockingThreat)
                    || PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.RoofOccupantHazard);
            }

            return PlanningStrategyApplicability.IsOppositeLane(planningState, decisionPoint)
                && CollectibleValuePolicy.HasPositiveCollectible(
                    planningState.Hamster,
                    decisionPoint.Chain);
        }

        /// <summary>
        /// Возвращает roof switch-lane action, если найдено безопасное окно с target roof support.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            if (!_planner.TryBuildModel(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    out RoofSwitchLaneModel model,
                    out string deadEndReason))
            {
                return string.IsNullOrEmpty(deadEndReason)
                    ? PlanningStrategyResult.NotApplicable()
                    : DeadEnd(deadEndReason);
            }

            return PlanningStrategyResult.FromAction(BuildAction(model));
        }

        /// <summary>
        /// Создает dead-end результат для применимой roof switch-lane strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(RoofSwitchLaneStrategy), message);
        }

        /// <summary>
        /// Создает planned action для выбранного roof switch-lane окна.
        /// </summary>
        private static PlannedAction BuildAction(RoofSwitchLaneModel model)
        {
            ObstacleSnapshot actionTarget = model.ObjectiveValue.HasValue
                ? model.ContextObstacle
                : model.TargetRoof;
            int actionTargetIndex = model.ObjectiveValue.HasValue
                ? model.ContextObstacleIndex
                : model.TargetRoofIndex;
            float triggerX = model.ContextObstacle.LeftX - model.FireShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                model.FireShift,
                model.FireWindowSample.FirstFireShift,
                model.FireWindowSample.LastFireShift);

            return new PlannedAction(
                BotActionKind.RoofSwitchLane,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: model.CompletionWorldShift,
                postFireWorldShift: SwitchLaneTiming.DecisionTravel,
                actionTargetIndex,
                targetObstacleInstanceId: actionTarget.InstanceId,
                triggerObstacleInstanceId: model.ContextObstacle.InstanceId,
                targetBottomLine: model.TargetBottomLine,
                energyCost: 0,
                description: $"Roof switch lane to {model.TargetRoof.ObstacleType} before {model.ContextObstacle.ObstacleType}",
                resultRoofSupportInstanceId: model.TargetRoof.InstanceId,
                isOppositeLaneEntry: true,
                triggerWindow: triggerWindow,
                collectibleObjectiveValue: model.ObjectiveValue);
        }
    }
}
