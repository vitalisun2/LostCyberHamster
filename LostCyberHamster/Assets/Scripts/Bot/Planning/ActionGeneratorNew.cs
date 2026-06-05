using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Generates role-based candidate actions through new decision points and planning strategies.
    /// </summary>
    public sealed class ActionGeneratorNew
    {
        private readonly IReadOnlyList<IPlanningStrategyNew> _strategies;
        private readonly IPlanningStrategyNew _switchLaneStrategy;
        private readonly DecisionPointDetectorNew _decisionPointDetector = new DecisionPointDetectorNew();

        /// <summary>
        /// Creates a role-based generator over the active new strategies.
        /// </summary>
        internal ActionGeneratorNew(IReadOnlyList<IPlanningStrategyNew> strategies)
        {
            _strategies = strategies ?? Array.Empty<IPlanningStrategyNew>();
            _switchLaneStrategy = FindStrategy(_strategies, BotActionKind.SwitchLane);
        }

        /// <summary>
        /// Generates actions available from the current planning state and world snapshot.
        /// </summary>
        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            var plannedActions = new List<PlannedAction>();
            if (planningState == null || worldSnapshot == null)
                return plannedActions;

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return plannedActions;

            bool currentBottomLine = planningState.IsOnBottomLine;

            bool hasCurrentDecisionPoint = _decisionPointDetector.TryDetect(
                    planningState,
                    projectedWorldSnapshot,
                    currentBottomLine,
                    out DecisionPointNew currentDecisionPoint);

            bool hasOppositeDecisionPoint = _decisionPointDetector.TryDetect(
                    planningState,
                    projectedWorldSnapshot,
                    !currentBottomLine,
                    out DecisionPointNew oppositeDecisionPoint);

            if (!hasCurrentDecisionPoint && !hasOppositeDecisionPoint)
            {
                LogNoDecisionPoint(planningState);
                return plannedActions;
            }

            if (hasCurrentDecisionPoint)
            {
                CollectActionsForDecisionPoint(
                    planningState,
                    projectedWorldSnapshot,
                    currentDecisionPoint,
                    plannedActions);
            }

            if (hasOppositeDecisionPoint)
            {
                CollectSwitchLaneEntryAction(
                    planningState,
                    projectedWorldSnapshot,
                    oppositeDecisionPoint,
                    plannedActions);
            }

            if (plannedActions.Count == 0 && hasCurrentDecisionPoint)
                LogNoActions(planningState, currentDecisionPoint);

            return plannedActions;
        }

        /// <summary>
        /// Requests actions from all role-based planning strategies for the decision point.
        /// </summary>
        private void CollectActionsForDecisionPoint(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPointNew decisionPoint,
            List<PlannedAction> plannedActions)
        {
            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                _strategies[strategyIndex].CollectActions(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint,
                    plannedActions);
            }
        }

        /// <summary>
        /// Adds the entry SwitchLane action for the opposite-lane branch.
        /// </summary>
        private void CollectSwitchLaneEntryAction(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPointNew oppositeDecisionPoint,
            List<PlannedAction> plannedActions)
        {
            if (_switchLaneStrategy == null)
                return;

            _switchLaneStrategy.CollectActions(
                planningState,
                projectedWorldSnapshot,
                oppositeDecisionPoint,
                plannedActions);
        }

        /// <summary>
        /// Finds a strategy by action kind.
        /// </summary>
        private static IPlanningStrategyNew FindStrategy(
            IReadOnlyList<IPlanningStrategyNew> strategies,
            BotActionKind actionKind)
        {
            if (strategies == null)
                return null;

            for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
            {
                IPlanningStrategyNew strategy = strategies[strategyIndex];
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
        private static void LogNoActions(PlanningState planningState, DecisionPointNew decisionPoint)
        {
            if (planningState == null || decisionPoint?.Chain == null)
                return;

            ObstacleChainNew chain = decisionPoint.Chain;
            ObstacleChainElementNew firstElement = chain.First;
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
