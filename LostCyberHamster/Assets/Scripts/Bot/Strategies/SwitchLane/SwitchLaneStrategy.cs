using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Собирает role-based кандидаты смены линии.
    /// </summary>
    internal sealed class SwitchLaneStrategy : IPlanningStrategy
    {
        private readonly SwitchLaneSpecification _specification;
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;
        private readonly SwitchLaneSimulator _simulator;

        public SwitchLaneStrategy()
        {
            _specification = new SwitchLaneSpecification();
            _fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _simulator = new SwitchLaneSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SwitchLaneExecutor(triggerGate);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SwitchLane;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет, может ли дорожный SwitchLane быть полезен для этой role-chain.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            if (!PlanningStrategyApplicability.HasContext(planningState, decisionPoint)
                || !PlanningStrategyApplicability.CanPlanGroundRun(planningState.Hamster))
            {
                return false;
            }

            if (PlanningStrategyApplicability.IsOppositeLane(planningState, decisionPoint))
            {
                return decisionPoint.Chain.HasAnyRequiredPlanningRole()
                    || CollectibleValuePolicy.HasPositiveCollectible(
                        planningState.Hamster,
                        decisionPoint.Chain);
            }

            return PlanningStrategyApplicability.IsCurrentLane(planningState, decisionPoint)
                && PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.BlockingThreat);
        }

        /// <summary>
        /// Возвращает действие смены линии для role-based точки решения.
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

            if (!TryResolveSwitchLaneTarget(
                    planningState,
                    decisionPoint,
                    out ObstacleSnapshot triggerObstacle,
                    out int triggerObstacleIndex,
                    out bool targetBottomLine,
                    out bool isEntryToOppositeLane))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    hamster,
                    triggerObstacle,
                    out float latestFireShift))
            {
                return DeadEnd(
                    isEntryToOppositeLane,
                    "Нет безопасного окна для смены линии: до препятствия не остается положительного интервала запуска.");
            }

            if (!IsCurrentLaneDamageThreat(hamster, triggerObstacle)
                && !_fireWindowCalculator.TryConstrainLatestFireShiftByCurrentLaneThreats(
                    worldSnapshot,
                    hamster,
                    latestFireShift,
                    out latestFireShift,
                    out string currentLaneThreatReason))
            {
                return DeadEnd(
                    isEntryToOppositeLane,
                    currentLaneThreatReason);
            }

            if (!_fireWindowCalculator.TrySelectRelevantFireWindowSample(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    out SwitchLaneFireWindowSample fireWindowSample))
            {
                return DeadEnd(
                    isEntryToOppositeLane,
                    BuildNoSwitchLaneSampleReason(worldSnapshot, hamster, targetBottomLine, latestFireShift));
            }

            PlannedAction action = BuildAction(
                planningState,
                triggerObstacle,
                triggerObstacleIndex,
                targetBottomLine,
                fireWindowSample,
                isEntryToOppositeLane);

            return PlanningStrategyResult.FromAction(action);
        }

        /// <summary>
        /// Возвращает true, если trigger уже является damaging obstacle на текущей линии хомяка.
        /// </summary>
        /// <remarks>
        /// `SwitchLane` строит два разных сценария: уход с текущей линии от blocking threat и вход на
        /// opposite lane ради collectable/route context. В первом сценарии deadline уже рассчитан по
        /// current-lane trigger-у в `TryGetLatestFireShift`, поэтому дополнительный scan всех угроз
        /// текущей линии не нужен. Во втором сценарии trigger лежит на целевой линии, поэтому caller
        /// должен отдельно ограничить окно ближайшей текущей угрозой через
        /// `TryConstrainLatestFireShiftByCurrentLaneThreats`.
        /// </remarks>
        private static bool IsCurrentLaneDamageThreat(
            HamsterSnapshot hamster,
            ObstacleSnapshot triggerObstacle)
        {
            return hamster != null
                && triggerObstacle != null
                && triggerObstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.DamagesOnGroundContact(triggerObstacle.ObstacleType);
        }

        /// <summary>
        /// Создает dead-end результат для применимой стратегии смены линии.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(bool isEntryToOppositeLane, string message)
        {
            string context = isEntryToOppositeLane
                ? "ход на другую линию"
                : "текущая линия";

            return PlanningStrategyResult.DeadEnd(nameof(SwitchLaneStrategy), $"{context}: {message}");
        }

        /// <summary>
        /// Уточняет причину отсутствия sample внутри safe-window смены линии.
        /// </summary>
        private string BuildNoSwitchLaneSampleReason(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift)
        {
            bool hasSafeIntervals = _fireWindowCalculator.HasAnySafeFireInterval(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift);

            return !hasSafeIntervals
                ? "Нет безопасного окна для смены линии: целевая линия перекрыта опасными препятствиями во всем допустимом интервале."
                : "Нет безопасного окна для смены линии: безопасный интервал слишком узкий для запуска действия.";
        }

        /// <summary>
        /// Определяет trigger obstacle для дорожной смены линии: угроза текущей линии или вход на другую линию.
        /// </summary>
        private bool TryResolveSwitchLaneTarget(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot triggerObstacle,
            out int triggerObstacleIndex,
            out bool targetBottomLine,
            out bool isEntryToOppositeLane)
        {
            triggerObstacle = null;
            triggerObstacleIndex = -1;
            targetBottomLine = false;
            isEntryToOppositeLane = false;

            if (planningState?.Hamster == null || decisionPoint?.Chain == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            bool chainBottomLine = decisionPoint.Chain.First.IsBottomLine;
            if (chainBottomLine != hamster.IsOnBottomLine)
            {
                if (!_specification.IsStateValid(planningState))
                    return false;

                triggerObstacle = decisionPoint.Chain.FirstObstacle;
                triggerObstacleIndex = decisionPoint.Chain.FirstIndex;
                targetBottomLine = chainBottomLine;
                isEntryToOppositeLane = true;
                return true;
            }

            if (!TryResolveBlockingThreat(
                    decisionPoint,
                    out ObstacleSnapshot blockingThreat,
                    out int blockingThreatIndex))
            {
                return false;
            }

            if (!_specification.IsSubjectValid(planningState, blockingThreat))
                return false;

            triggerObstacle = blockingThreat;
            triggerObstacleIndex = blockingThreatIndex;
            targetBottomLine = !hamster.IsOnBottomLine;
            return true;
        }

        /// <summary>
        /// Ищет первую блокирующую угрозу в текущей focus-chain.
        /// </summary>
        private static bool TryResolveBlockingThreat(
            DecisionPoint decisionPoint,
            out ObstacleSnapshot blockingThreat,
            out int blockingThreatIndex)
        {
            blockingThreat = null;
            blockingThreatIndex = -1;

            if (decisionPoint?.Chain == null)
                return false;

            if (!decisionPoint.Chain.TryFindFirstWithRole(
                    ObstacleRole.BlockingThreat,
                    out ObstacleChainElement blockingThreatElement,
                    out _))
            {
                return false;
            }

            blockingThreat = blockingThreatElement.Obstacle;
            blockingThreatIndex = blockingThreatElement.WorldIndex;
            return true;
        }

        /// <summary>
        /// Создает действие смены линии для выбранного момента запуска.
        /// </summary>
        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            int triggerObstacleIndex,
            bool targetBottomLine,
            SwitchLaneFireWindowSample fireWindowSample,
            bool isEntryToOppositeLane)
        {
            float fireShift = fireWindowSample.FireShift;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                fireWindowSample.FirstFireShift,
                fireWindowSample.LastFireShift);

            return new PlannedAction(
                BotActionKind.SwitchLane,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + SwitchLaneTiming.DecisionTravel,
                postFireWorldShift: SwitchLaneTiming.DecisionTravel,
                triggerObstacleIndex,
                targetObstacleInstanceId: triggerObstacle.InstanceId,
                targetBottomLine: targetBottomLine,
                energyCost: 0,
                description: isEntryToOppositeLane
                    ? $"Switch lane entry before {triggerObstacle.ObstacleType}"
                    : $"Switch lane before {triggerObstacle.ObstacleType}",
                isOppositeLaneEntry: isEntryToOppositeLane,
                triggerWindow: triggerWindow,
                collectibleObjectiveValue: ResolveSwitchCollectibleValue(
                    planningState,
                    triggerObstacle,
                    targetBottomLine,
                    completionWorldShift: fireShift + SwitchLaneTiming.DecisionTravel,
                    isEntryToOppositeLane));
        }

        /// <summary>
        /// Возвращает planning-ценность collectable, если вход на opposite lane фактически подбирает его до completion.
        /// </summary>
        /// <remarks>
        /// `SwitchLane` может быть не только defensive action, но и способом забрать bonus на соседней
        /// линии. Value записывается в `PlannedAction` только для opposite-lane entry, когда trigger
        /// расположен на target lane, collectable полезен в текущем projected состоянии, и его X-позиция
        /// будет достигнута до завершения switch. В остальных случаях action остается обычным
        /// перестроением без collectible objective, чтобы evaluator не награждал маневр за бонус,
        /// который route ещё не подобрал.
        /// </remarks>
        private static CollectibleObjectiveValue ResolveSwitchCollectibleValue(
            PlanningState planningState,
            ObstacleSnapshot triggerObstacle,
            bool targetBottomLine,
            float completionWorldShift,
            bool isEntryToOppositeLane)
        {
            if (!isEntryToOppositeLane
                || planningState?.Hamster == null
                || triggerObstacle == null
                || triggerObstacle.IsBottomLine != targetBottomLine
                || !CollectibleValuePolicy.TryGetPositiveValue(
                    planningState.Hamster,
                    triggerObstacle,
                    out CollectibleObjectiveValue objectiveValue))
            {
                return CollectibleObjectiveValue.None;
            }

            float pickupShift = triggerObstacle.LeftX - planningState.Hamster.HamsterRightX;
            if (pickupShift < 0f)
                pickupShift = 0f;

            return pickupShift <= completionWorldShift
                ? objectiveValue
                : CollectibleObjectiveValue.None;
        }

    }
}
