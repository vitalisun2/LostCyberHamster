using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Диагностика strategy/action generation.
    /// Стратегии и generator передают сюда уже вычисленные причины отказа, logger их не выводит заново.
    /// </summary>
    internal static class BotStrategyDiagnostics
    {
        public static void LogDeadEndContext(
            PlanningState planningState,
            DecisionPoint currentDecisionPoint,
            DecisionPoint oppositeDecisionPoint,
            bool hasCurrentDecisionPoint,
            bool hasOppositeDecisionPoint,
            int deadEndReasonCount)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Strategy, BotDiagnosticLevel.Verbose)
                || planningState?.Hamster == null)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Verbose,
                "[Bot DEAD_END_DIAG] " +
                $"lane={(planningState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={planningState.Hamster.HamsterState} " +
                $"energy={planningState.Hamster.Energy} " +
                $"nextObstacleIndex={planningState.NextObstacleIndex} " +
                $"projection={planningState.ProjectionWorldShift:F2} " +
                $"hasCurrent={hasCurrentDecisionPoint} " +
                $"current={FormatDecisionPoint(currentDecisionPoint)} " +
                $"hasOpposite={hasOppositeDecisionPoint} " +
                $"opposite={FormatDecisionPoint(oppositeDecisionPoint)} " +
                $"reasons={deadEndReasonCount}");
        }

        public static void LogSuperFallbackDecision(
            PlannedAction action,
            bool hasSameTargetJumpOn,
            bool hasSameTargetAndTriggerJumpOn,
            bool removed,
            string existingActions)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Strategy, BotDiagnosticLevel.Verbose)
                || action?.Kind != BotActionKind.SuperJumpOn
                || !hasSameTargetJumpOn)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Verbose,
                "[Bot SUPER_FALLBACK_DIAG] " +
                $"action={action.Kind}/cost={action.EnergyCost}/target={FormatNullable(action.TargetObstacleInstanceId)}" +
                $"/trigger={FormatNullable(action.TriggerObstacleInstanceId)} " +
                $"hasSameTargetJumpOn={hasSameTargetJumpOn} " +
                $"hasSameTargetAndTriggerJumpOn={hasSameTargetAndTriggerJumpOn} " +
                $"removed={removed} " +
                $"existing={existingActions}");
        }

        public static void LogNoDecision(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Strategy, BotDiagnosticLevel.Verbose)
                || planningState?.Hamster == null
                || obstacle == null)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Verbose,
                $"[Bot PLAN NEW] NO_DECISION firstObstacle={obstacle.ObstacleType} " +
                $"x=[{obstacle.LeftX:F2},{obstacle.RightX:F2}] " +
                $"lane={(obstacle.IsBottomLine ? "bottom" : "top")} " +
                $"hamsterLane={(planningState.IsOnBottomLine ? "bottom" : "top")}");
        }

        private static string FormatDecisionPoint(DecisionPoint decisionPoint)
        {
            if (decisionPoint?.Kind == DecisionPointKind.MovingBoundary)
                return $"movingBoundary={decisionPoint.MovingBoundaryKind}";

            if (decisionPoint?.Chain == null)
                return "none";

            ObstacleChain chain = decisionPoint.Chain;
            return $"[{chain.LeftX:F2},{chain.RightX:F2}] " +
                   $"count={chain.Count} " +
                   $"items={FormatChainItems(chain)}";
        }

        private static string FormatChainItems(ObstacleChain chain)
        {
            if (chain == null)
                return "none";

            const int maxItems = 4;
            int count = chain.Count < maxItems ? chain.Count : maxItems;
            var parts = new List<string>(count + 1);
            for (int index = 0; index < count; index++)
            {
                ObstacleChainElement element = chain.Elements[index];
                ObstacleSnapshot obstacle = element.Obstacle;
                parts.Add(
                    $"{element.WorldIndex}:{obstacle.ObstacleType}#" +
                    $"{obstacle.InstanceId} " +
                    $"x=[{obstacle.LeftX:F2},{obstacle.RightX:F2}] " +
                    $"lane={(obstacle.IsBottomLine ? "bottom" : "top")} " +
                    $"roles={FormatRoles(element.Roles)}");
            }

            if (chain.Count > maxItems)
                parts.Add("...");

            return string.Join(";", parts);
        }

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

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }
    }
}
