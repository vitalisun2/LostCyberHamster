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
        /// <summary>
        /// Определяет target context для defensive и reward roof switch-lane сценариев.
        /// </summary>
        private readonly RoofSwitchLaneTargetResolver _targetResolver;

        /// <summary>
        /// Находит безопасное окно запуска и roof support для target context.
        /// </summary>
        private readonly RoofSwitchLaneWindowFinder _windowFinder;

        public RoofSwitchLaneStrategy()
        {
            // Создает planning-зависимости.
            var fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _targetResolver = new RoofSwitchLaneTargetResolver();
            _windowFinder = new RoofSwitchLaneWindowFinder(fireWindowCalculator);

            // Создает execution-зависимости.
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new RoofSwitchLaneExecutor(triggerGate);
            Simulator = new RoofSwitchLaneSimulator();
        }

        /// <summary>
        /// Возвращает тип действия, создаваемого стратегией.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.RoofSwitchLane;

        /// <summary>
        /// Возвращает runtime executor для roof switch-lane action.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Возвращает simulator planning-перехода после roof switch-lane.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Проверяет две причины применимости: defensive уход с текущей roof lane и reward route на opposite roof lane.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            // Проверяет базовую применимость.
            if (!PlanningStrategyApplicability.HasContext(planningState, decisionPoint)
                || !PlanningStrategyApplicability.CanPlanRoofRun(planningState.Hamster))
            {
                return false;
            }

            // Проверяет defensive-сценарий текущей линии.
            if (PlanningStrategyApplicability.IsCurrentLane(planningState, decisionPoint))
            {
                return PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.BlockingThreat)
                    || PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.RoofOccupantHazard);
            }

            // Проверяет reward-сценарий другой roof-line.
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
            // Проверяет обязательный контекст.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            // Определяет сценарий roof switch-lane.
            if (!_targetResolver.TryResolve(
                    planningState,
                    decisionPoint,
                    out RoofSwitchLaneTarget target))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            // Находит безопасное окно.
            if (!_windowFinder.TryFind(
                    planningState,
                    worldSnapshot,
                    target,
                    out RoofSwitchLaneWindow window,
                    out string deadEndReason))
            {
                return string.IsNullOrEmpty(deadEndReason)
                    ? PlanningStrategyResult.NotApplicable()
                    : DeadEnd(deadEndReason);
            }

            // Возвращает готовый action.
            return PlanningStrategyResult.FromAction(BuildAction(
                planningState.Hamster,
                target,
                window));
        }

        /// <summary>
        /// Создает dead-end результат для применимой roof switch-lane strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(RoofSwitchLaneStrategy), message);
        }

        /// <summary>
        /// Возвращает collectable objective только если switch сам успевает подобрать context collectable.
        /// </summary>
        private static CollectibleObjectiveValue ResolveImmediateCollectibleObjective(
            HamsterSnapshot hamster,
            ObstacleSnapshot contextObstacle,
            float completionWorldShift,
            CollectibleObjectiveValue objectiveValue)
        {
            // Проверяет наличие collectable objective.
            if (hamster == null
                || contextObstacle == null
                || !objectiveValue.HasValue)
            {
                return CollectibleObjectiveValue.None;
            }

            // Рассчитывает shift до pickup.
            float pickupShift = contextObstacle.LeftX - hamster.HamsterRightX;
            if (pickupShift < 0f)
                pickupShift = 0f;

            // Возвращает objective только при достижении collectable.
            return pickupShift <= completionWorldShift
                ? objectiveValue
                : CollectibleObjectiveValue.None;
        }

        /// <summary>
        /// Создает planned action для выбранного roof switch-lane окна.
        /// </summary>
        private static PlannedAction BuildAction(
            HamsterSnapshot hamster,
            RoofSwitchLaneTarget target,
            RoofSwitchLaneWindow window)
        {
            // Рассчитывает timing action-а.
            float fireShift = window.FireWindowSample.FireShift;
            float completionWorldShift = fireShift + SwitchLaneTiming.DecisionTravel;

            // Определяет реально выполняемый collectible objective.
            CollectibleObjectiveValue objectiveValue = ResolveImmediateCollectibleObjective(
                hamster,
                target.ContextObstacle,
                completionWorldShift,
                target.ObjectiveValue);

            // Выбирает target для executor-а и метрик.
            ObstacleSnapshot actionTarget = objectiveValue.HasValue
                ? target.ContextObstacle
                : window.TargetRoof;
            int actionTargetIndex = objectiveValue.HasValue
                ? target.ContextObstacleIndex
                : window.TargetRoofIndex;

            // Формирует trigger window.
            float triggerX = target.ContextObstacle.LeftX - fireShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                window.FireWindowSample.FirstFireShift,
                window.FireWindowSample.LastFireShift);

            // Создает planning action.
            return new PlannedAction(
                BotActionKind.RoofSwitchLane,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: completionWorldShift,
                postFireWorldShift: SwitchLaneTiming.DecisionTravel,
                actionTargetIndex,
                targetObstacleInstanceId: actionTarget.InstanceId,
                triggerObstacleInstanceId: target.ContextObstacle.InstanceId,
                targetBottomLine: target.TargetBottomLine,
                energyCost: 0,
                description: $"Roof switch lane to {window.TargetRoof.ObstacleType} before {target.ContextObstacle.ObstacleType}",
                resultRoofSupportInstanceId: window.TargetRoof.InstanceId,
                isOppositeLaneEntry: true,
                triggerWindow: triggerWindow,
                collectibleObjectiveValue: objectiveValue);
        }
    }
}
