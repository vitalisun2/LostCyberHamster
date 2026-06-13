using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Результат генерации actions для одного planning state.
    /// </summary>
    internal sealed class ActionGenerationResult
    {
        public ActionGenerationResult(
            IReadOnlyList<PlannedAction> actions,
            IReadOnlyList<StrategyDeadEndReason> deadEndReasons)
        {
            Actions = actions ?? Array.Empty<PlannedAction>();
            DeadEndReasons = deadEndReasons ?? Array.Empty<StrategyDeadEndReason>();
        }

        public IReadOnlyList<PlannedAction> Actions { get; }
        public IReadOnlyList<StrategyDeadEndReason> DeadEndReasons { get; }
        public bool HasDeadEndReasons => DeadEndReasons.Count > 0;

        public static ActionGenerationResult Empty()
        {
            return new ActionGenerationResult(
                Array.Empty<PlannedAction>(),
                Array.Empty<StrategyDeadEndReason>());
        }
    }

    /// <summary>
    /// Generates role-based candidate actions through new decision points and planning strategies.
    /// </summary>
    public sealed class ActionGenerator
    {
        private readonly IReadOnlyList<IPlanningStrategy> _strategies;
        private readonly IPlanningStrategy _switchLaneStrategy;
        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();

        /// <summary>
        /// Creates a role-based generator over the active new strategies.
        /// </summary>
        internal ActionGenerator(IReadOnlyList<IPlanningStrategy> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategy>();
            _switchLaneStrategy = FindStrategy(_strategies, BotActionKind.SwitchLane);
        }

        /// <summary>
        /// Generates actions available from the current planning state and world snapshot.
        /// </summary>
        internal ActionGenerationResult Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            var plannedActions = new List<PlannedAction>();
            var deadEndReasons = new List<StrategyDeadEndReason>();
            if (planningState == null || worldSnapshot == null)
                return ActionGenerationResult.Empty();

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return ActionGenerationResult.Empty();

            bool currentBottomLine = planningState.IsOnBottomLine;

            bool hasCurrentDecisionPoint = _decisionPointDetector.TryDetect(
                    planningState,
                    projectedWorldSnapshot,
                    currentBottomLine,
                    out DecisionPoint currentDecisionPoint);

            bool hasOppositeDecisionPoint = _decisionPointDetector.TryDetect(
                    planningState,
                    projectedWorldSnapshot,
                    !currentBottomLine,
                    out DecisionPoint oppositeDecisionPoint);

            if (!hasCurrentDecisionPoint && !hasOppositeDecisionPoint)
            {
                LogNoDecisionPoint(planningState);
                return new ActionGenerationResult(
                    plannedActions,
                    deadEndReasons);
            }

            if (hasCurrentDecisionPoint)
            {
                CollectActionsForDecisionPoint(
                    planningState,
                    projectedWorldSnapshot,
                    currentDecisionPoint,
                    plannedActions,
                    deadEndReasons);
            }

            if (hasOppositeDecisionPoint)
            {
                CollectSwitchLaneEntryAction(
                    planningState,
                    projectedWorldSnapshot,
                    oppositeDecisionPoint,
                    plannedActions,
                    deadEndReasons);
            }

            if (plannedActions.Count == 0 && hasCurrentDecisionPoint)
                LogNoActions(planningState, currentDecisionPoint);

            return new ActionGenerationResult(
                plannedActions,
                deadEndReasons);
        }

        /// <summary>
        /// Requests actions from all role-based planning strategies for the decision point.
        /// </summary>
        private void CollectActionsForDecisionPoint(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                PlanningStrategyResult result = _strategies[strategyIndex].CollectActions(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint);

                ApplyStrategyResult(
                    result,
                    plannedActions,
                    deadEndReasons);
            }
        }

        /// <summary>
        /// Adds the entry SwitchLane action for the opposite-lane branch.
        /// </summary>
        private void CollectSwitchLaneEntryAction(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint oppositeDecisionPoint,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (_switchLaneStrategy == null)
                return;

            PlanningStrategyResult result = _switchLaneStrategy.CollectActions(
                planningState,
                projectedWorldSnapshot,
                oppositeDecisionPoint);

            ApplyStrategyResult(
                result,
                plannedActions,
                deadEndReasons);
        }

        /// <summary>
        /// Добавляет результат одной strategy в общий generation result.
        /// </summary>
        private static void ApplyStrategyResult(
            PlanningStrategyResult result,
            List<PlannedAction> plannedActions,
            List<StrategyDeadEndReason> deadEndReasons)
        {
            if (result == null || !result.IsApplicable)
                return;

            for (int actionIndex = 0; actionIndex < result.Actions.Count; actionIndex++)
            {
                PlannedAction action = result.Actions[actionIndex];
                if (action != null)
                    plannedActions.Add(action);
            }

            if (result.HasDeadEndReason)
                deadEndReasons.Add(result.DeadEndReason);
        }

        /// <summary>
        /// Finds a strategy by action kind.
        /// </summary>
        private static IPlanningStrategy FindStrategy(
            IReadOnlyList<IPlanningStrategy> strategies,
            BotActionKind actionKind)
        {
            if (strategies == null)
                return null;

            for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy != null && strategy.ActionKind == actionKind)
                    return strategy;
            }

            return null;
        }

        /// <summary>
        /// Logs the absence of a role-based decision point.
        /// </summary>
        private static void LogNoDecisionPoint(PlanningState planningState)
        {
            if (planningState?.Hamster == null)
                return;

            DebugManager.DiagLogVerbose(
                $"[Bot PLAN NEW] NO_DECISION " +
                $"nextObstacleIndex={planningState.NextObstacleIndex} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        /// <summary>
        /// Logs a role-based decision point that produced no strategy actions.
        /// </summary>
        private static void LogNoActions(PlanningState planningState, DecisionPoint decisionPoint)
        {
            if (planningState == null || decisionPoint?.Chain == null)
                return;

            ObstacleChain chain = decisionPoint.Chain;
            ObstacleChainElement firstElement = chain.First;
            ObstacleSnapshot firstObstacle = firstElement.Obstacle;

            DebugManager.DiagLogVerbose(
                $"[Bot PLAN NEW] NO_ACTIONS firstObstacle={firstObstacle.ObstacleType} " +
                $"roles={FormatRoles(firstElement.Roles)} " +
                $"chainCount={chain.Count} " +
                $"chainLeftX={chain.LeftX:F2} chainRightX={chain.RightX:F2} " +
                $"firstLeftX={firstObstacle.LeftX:F2} firstRightX={firstObstacle.RightX:F2} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        /// <summary>
        /// Formats obstacle roles for the diagnostic log.
        /// </summary>
        private static string FormatRoles(IReadOnlyCollection<ObstacleRole> roles)
        {
            if (roles == null || roles.Count == 0)
                return "none";

            var roleNames = new List<string>(roles.Count);
            foreach (ObstacleRole role in roles)
                roleNames.Add(role.ToString());

            roleNames.Sort(StringComparer.Ordinal);
            return string.Join("|", roleNames);
        }
    }
}
