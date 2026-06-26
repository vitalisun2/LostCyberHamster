using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Диагностика runtime-исполнения bot actions.
    /// Используется в executor-ах: fire/complete/cancel и trigger-gate отказы.
    /// </summary>
    internal static class BotExecutionDiagnostics
    {
        public static void LogFire(PlannedAction action, float obstacleLeftX, string extra = null)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Execution))
                return;

            string window = action.TriggerWindow.HasValue
                ? $"window=[{action.TriggerWindow.Value.EarliestTriggerX:F2},{action.TriggerWindow.Value.LatestTriggerX:F2}] "
                : string.Empty;
            float triggerOvershoot = action.TriggerX - obstacleLeftX;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Execution,
                BotDiagnosticLevel.Essential,
                $"[Bot EXEC] FIRE kind={action.Kind} " +
                $"{FormatActionIds(action)} " +
                $"triggerX={action.TriggerX:F2} renderX={action.RenderWorldX:F2} obstacleLeftX={obstacleLeftX:F2} " +
                $"triggerOvershoot={triggerOvershoot:F2} {window}" +
                $"{extra ?? string.Empty}" +
                $"desc={action.Description}");
        }

        public static void LogComplete(PlannedAction action, HamsterStateEnum state)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Execution))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Execution,
                BotDiagnosticLevel.Essential,
                $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                $"{FormatActionIds(action)} " +
                $"state={state} " +
                $"desc={action.Description}");
        }

        public static void LogComplete(PlannedAction action, bool isBottomLine)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Execution))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Execution,
                BotDiagnosticLevel.Essential,
                $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                $"{FormatActionIds(action)} " +
                $"lane={(isBottomLine ? "bottom" : "top")} " +
                $"desc={action.Description}");
        }

        public static void LogCancel(PlannedAction action, string extra)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Execution))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Execution,
                BotDiagnosticLevel.Essential,
                $"[Bot EXEC] CANCEL kind={action.Kind} " +
                $"{FormatActionIds(action)} " +
                $"{extra} " +
                $"desc={action.Description}");
        }

        /// <summary>
        /// Пишет отказ trigger gate. Ставится сразу после ActionTriggerGate.Check, когда action не fired.
        /// </summary>
        public static void LogTriggerGateResult(
            string tag,
            PlannedAction action,
            Hamster hamster,
            ActionFireResult result,
            float obstacleLeftX,
            string diagnosticReason,
            BotDiagnosticLevel level = BotDiagnosticLevel.Verbose)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Execution, level))
                return;

            string window = action.TriggerWindow.HasValue
                ? $"window=[{action.TriggerWindow.Value.EarliestTriggerX:F2},{action.TriggerWindow.Value.LatestTriggerX:F2}] "
                : string.Empty;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Execution,
                level,
                $"[{tag}] result={result} " +
                $"{FormatActionIds(action)} " +
                $"triggerX={action.TriggerX:F2} obstacleLeftX={obstacleLeftX:F2} " +
                $"{window}" +
                $"energy={hamster.Energy.Value} state={hamster.HamsterState.Value} " +
                $"reason={diagnosticReason ?? "none"} desc={action.Description}");
        }

        private static string FormatActionIds(PlannedAction action)
        {
            return $"targetId={FormatNullable(action?.TargetObstacleInstanceId)} " +
                   $"triggerId={FormatNullable(action?.TriggerObstacleInstanceId)}";
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }
    }
}
