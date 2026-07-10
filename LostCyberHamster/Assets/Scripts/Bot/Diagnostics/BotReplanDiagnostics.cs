using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Диагностика replan lifecycle, установленного плана и dead-end summary.
    /// Методы вызываются из RuntimeBotController после принятия planning-решения.
    /// </summary>
    internal static class BotReplanDiagnostics
    {
        private const int MaxDeadEndCandidates = 6;
        private const int MaxDeadEndReasons = 4;

        public static void LogPlan(BotPlan plan, string formattedPlanChain)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Replan)
                || plan == null
                || !plan.HasActions)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.Replan,
                BotDiagnosticLevel.Essential,
                $"[Bot PLAN] {formattedPlanChain}");
        }

        /// <summary>
        /// Формирует компактный отчёт только для случая, когда successful-веток нет
        /// и planner выбрал наиболее далеко продвинувшийся dead-end prefix.
        /// </summary>
        public static bool TryFormatDeadEndSelection(
            PlanningDeadEndSelection selection,
            out string formattedReport)
        {
            formattedReport = null;
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Replan))
                return false;

            IReadOnlyList<PlanningDeadEndBranch> candidates = selection?.Candidates;
            int selectedIndex = FindSelectedCandidateIndex(selection);
            if (candidates == null || candidates.Count == 0 || selectedIndex < 0)
                return false;

            var builder = new StringBuilder(512);
            builder.Append("[Bot DEAD_END_SELECTION] successful=0 deadEnds=")
                .Append(candidates.Count)
                .AppendLine();

            int leadingCandidateCount = candidates.Count < MaxDeadEndCandidates
                ? candidates.Count
                : MaxDeadEndCandidates;
            bool appendSelectedSeparately = selectedIndex >= leadingCandidateCount;
            if (appendSelectedSeparately)
                leadingCandidateCount--;

            for (int candidateIndex = 0; candidateIndex < leadingCandidateCount; candidateIndex++)
                AppendDeadEndCandidate(builder, candidates[candidateIndex], candidateIndex + 1);

            if (appendSelectedSeparately)
                AppendDeadEndCandidate(builder, candidates[selectedIndex], selectedIndex + 1);

            int displayedCandidateCount = leadingCandidateCount + (appendSelectedSeparately ? 1 : 0);
            if (candidates.Count > displayedCandidateCount)
            {
                builder.Append("[Bot DEAD_END_SELECTION] omitted=")
                    .Append(candidates.Count - displayedCandidateCount)
                    .AppendLine();
            }

            builder.Append("[Bot DEAD_END_SELECTION] selected=#")
                .Append(selectedIndex + 1)
                .Append(" reason=maxFailureProjection");
            formattedReport = builder.ToString();
            return true;
        }

        public static void LogDeadEndSelection(string formattedReport)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Replan)
                || string.IsNullOrWhiteSpace(formattedReport))
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.Replan,
                BotDiagnosticLevel.Essential,
                formattedReport);
        }

        public static void LogPlanBuildResult(
            PlanBuildResult buildResult,
            string formattedReplanReasons,
            string formattedPlanChain,
            string formattedDepth,
            string formattedNextObstacleIndex,
            string formattedProjection)
        {
            BotPlan plan = buildResult?.Plan;
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Replan, BotDiagnosticLevel.Verbose)
                || plan == null
                || !plan.HasActions)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.Replan,
                BotDiagnosticLevel.Verbose,
                "[Bot PLAN_RESULT_DIAG] " +
                $"reasons={formattedReplanReasons} " +
                $"hasDeadEnd={buildResult.HasDeadEnd} " +
                $"reportDepth={formattedDepth} " +
                $"reportNext={formattedNextObstacleIndex} " +
                $"reportProjection={formattedProjection} " +
                $"plan={formattedPlanChain}");
        }

        public static void LogAsyncHeadWindow(
            string formattedReplanReasons,
            float snapshotAgeSeconds,
            float snapshotAgeWorldShift,
            PlannedAction head,
            int? triggerObstacleInstanceId,
            float liveObstacleLeftX,
            float afterCloseDelta,
            string formattedPlanChain)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Replan, BotDiagnosticLevel.Verbose))
                return;

            ActionTriggerWindow triggerWindow = head.TriggerWindow.Value;
            BotDiagnostics.Log(
                BotDiagnosticCategory.Replan,
                BotDiagnosticLevel.Verbose,
                "[Bot ASYNC_HEAD_WINDOW_DIAG] " +
                $"reasons={formattedReplanReasons} " +
                $"ageSeconds={snapshotAgeSeconds:F3} ageShift={snapshotAgeWorldShift:F3} " +
                $"kind={head.Kind} targetId={FormatNullable(head.TargetObstacleInstanceId)} " +
                $"triggerId={FormatNullable(triggerObstacleInstanceId)} " +
                $"triggerX={head.TriggerX:F2} window=[{triggerWindow.EarliestTriggerX:F2},{triggerWindow.LatestTriggerX:F2}] " +
                $"liveObstacleLeftX={liveObstacleLeftX:F2} afterCloseDelta={afterCloseDelta:F3} " +
                $"plan={formattedPlanChain}");
        }

        public static void LogDeadEndHeader(string formattedReplanReasons, string deadEndDetails)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.DeadEnd))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.DeadEnd,
                BotDiagnosticLevel.Essential,
                $"[Bot DEAD_END] confirmed=true reason={formattedReplanReasons} " +
                deadEndDetails);
            BotDiagnostics.Log(
                BotDiagnosticCategory.DeadEnd,
                BotDiagnosticLevel.Essential,
                "[Bot DEAD_END] causes:");
        }

        public static void LogDeadEndCause(string cause)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.DeadEnd))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.DeadEnd,
                BotDiagnosticLevel.Essential,
                $"[Bot DEAD_END] {cause}");
        }

        public static void LogDeadEndWithoutReasons()
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.DeadEnd))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.DeadEnd,
                BotDiagnosticLevel.Essential,
                "[Bot DEAD_END] Применимые стратегии не вернули действия, но dead-end причины не собраны.");
        }

        public static void LogPatternSpawn(
            int patternIndex,
            string patternName,
            string obstacleIds)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Pattern))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Pattern,
                BotDiagnosticLevel.Essential,
                $"[Bot PATTERN] SPAWN patternIndex={patternIndex} pattern={patternName} " +
                $"obstacleIds={obstacleIds}");
        }

        public static void LogPatternDetail(
            int patternIndex,
            string patternName,
            string obstacleDetails)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Pattern, BotDiagnosticLevel.Verbose))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Pattern,
                BotDiagnosticLevel.Verbose,
                $"[Bot PATTERN_DETAIL] patternIndex={patternIndex} pattern={patternName} " +
                $"obstacles={obstacleDetails}");
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }

        private static int FindSelectedCandidateIndex(PlanningDeadEndSelection selection)
        {
            IReadOnlyList<PlanningDeadEndBranch> candidates = selection?.Candidates;
            if (candidates == null || selection.SelectedCandidate == null)
                return -1;

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (ReferenceEquals(candidates[candidateIndex], selection.SelectedCandidate))
                    return candidateIndex;
            }

            return -1;
        }

        private static void AppendDeadEndCandidate(
            StringBuilder builder,
            PlanningDeadEndBranch candidate,
            int candidateNumber)
        {
            builder.Append("[Bot DEAD_END_SELECTION] #")
                .Append(candidateNumber)
                .Append(" actions=");
            AppendActionChain(builder, candidate?.Branch?.Actions);
            builder.Append(" failAt=");
            AppendFailureProjection(builder, candidate?.Report);
            builder.Append(" reasons=");
            AppendDeadEndReasons(builder, candidate?.Report?.Reasons);
            builder.AppendLine();
        }

        private static void AppendActionChain(
            StringBuilder builder,
            IReadOnlyList<PlannedAction> actions)
        {
            if (actions == null || actions.Count == 0)
            {
                builder.Append("none");
                return;
            }

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                if (actionIndex > 0)
                    builder.Append(" -> ");

                PlannedAction action = actions[actionIndex];
                if (action == null)
                {
                    builder.Append("null");
                    continue;
                }

                builder.Append(action.Kind);
                if (!action.FulfillsCollectibleObjective)
                    continue;

                builder.Append('[')
                    .Append(action.CollectibleObjectiveValue.Kind)
                    .Append(':')
                    .Append(action.CollectibleObjectiveValue.EffectiveGain)
                    .Append(']');
            }
        }

        private static void AppendFailureProjection(
            StringBuilder builder,
            PlanningDeadEndReport report)
        {
            if (report == null)
            {
                builder.Append("none");
                return;
            }

            builder.Append(report.ProjectionWorldShift.ToString("F2", CultureInfo.InvariantCulture));
        }

        private static void AppendDeadEndReasons(
            StringBuilder builder,
            IReadOnlyList<StrategyDeadEndReason> reasons)
        {
            if (reasons == null || reasons.Count == 0)
            {
                builder.Append("none");
                return;
            }

            int reasonCount = reasons.Count < MaxDeadEndReasons ? reasons.Count : MaxDeadEndReasons;
            for (int reasonIndex = 0; reasonIndex < reasonCount; reasonIndex++)
            {
                if (reasonIndex > 0)
                    builder.Append("; ");

                StrategyDeadEndReason reason = reasons[reasonIndex];
                if (reason == null)
                {
                    builder.Append("null");
                    continue;
                }

                builder.Append(reason.StrategyName)
                    .Append(':')
                    .Append(reason.Message);
            }

            if (reasons.Count > reasonCount)
            {
                builder.Append("; ... total=")
                    .Append(reasons.Count);
            }
        }
    }
}
