using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Collects role-based SwitchLane candidates for the new planning path.
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
        /// Collects valid lane-switch actions for a role-based decision point.
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
                return DeadEnd("Нет безопасного окна для смены линии: до препятствия не остается положительного интервала запуска.");
            }

            IReadOnlyList<float> selectionRatios = GetSelectionRatios(planningState);
            IReadOnlyList<SwitchLaneFireWindowSample> fireWindowSamples =
                _fireWindowCalculator.CollectFireWindowSamples(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    selectionRatios);

            if (fireWindowSamples.Count == 0)
                return DeadEnd(BuildNoSwitchLaneSampleReason(worldSnapshot, hamster, targetBottomLine, latestFireShift));

            var actions = new List<PlannedAction>(fireWindowSamples.Count);
            for (int sampleIndex = 0; sampleIndex < fireWindowSamples.Count; sampleIndex++)
            {
                SwitchLaneFireWindowSample fireWindowSample = fireWindowSamples[sampleIndex];
                actions.Add(BuildAction(
                    planningState,
                    triggerObstacle,
                    triggerObstacleIndex,
                    targetBottomLine,
                    fireWindowSample,
                    isEntryToOppositeLane));
            }

            return PlanningStrategyResult.FromActions(actions);
        }

        /// <summary>
        /// Создает dead-end результат для применимой стратегии смены линии.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(SwitchLaneStrategy), message);
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
            List<SafeInterval> safeIntervals = _fireWindowCalculator.CollectSafeFireIntervals(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift);

            return safeIntervals.Count == 0
                ? "Нет безопасного окна для смены линии: целевая линия перекрыта опасными препятствиями во всем допустимом интервале."
                : "Нет безопасного окна для смены линии: безопасный интервал слишком узкий для запуска действия.";
        }

        /// <summary>
        /// Resolves the road SwitchLane target: current-lane threat or opposite-lane entry.
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
                if (!_specification.IsSatisfiedBy(planningState))
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

            if (!_specification.IsSatisfiedBy(planningState, blockingThreat))
                return false;

            triggerObstacle = blockingThreat;
            triggerObstacleIndex = blockingThreatIndex;
            targetBottomLine = !hamster.IsOnBottomLine;
            return true;
        }

        /// <summary>
        /// Tries to select the first blocking threat from the current focus chain.
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
        /// Builds a planned lane-switch action for the selected fire moment.
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
                triggerWindow: triggerWindow);
        }

        /// <summary>
        /// Returns safe-window selection ratios for role-based SwitchLane.
        /// </summary>
        private static IReadOnlyList<float> GetSelectionRatios(PlanningState planningState)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            if (JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster))
                return new[]
                {
                    SwitchLaneTiming.EarlyWindowSelectionRatio,
                    SwitchLaneTiming.MidWindowSelectionRatio
                };

            return new[]
            {
                SwitchLaneTiming.MidWindowSelectionRatio
            };
        }
    }
}
