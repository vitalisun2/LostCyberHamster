using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Диагностика replan lifecycle, установленного плана и dead-end summary.
    /// Методы вызываются из RuntimeBotController после принятия planning-решения.
    /// </summary>
    internal static class BotReplanDiagnostics
    {
        public static void LogPlan(BotPlan plan, string formattedPlanChain)
        {
            if (plan == null || !plan.HasActions)
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Replan,
                BotDiagnosticLevel.Essential,
                $"[Bot PLAN] {formattedPlanChain}");
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
            if (plan == null || !plan.HasActions)
                return;

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
            BotDiagnostics.Log(
                BotDiagnosticCategory.DeadEnd,
                BotDiagnosticLevel.Essential,
                $"[Bot DEAD_END] {cause}");
        }

        public static void LogDeadEndWithoutReasons()
        {
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
    }
}
