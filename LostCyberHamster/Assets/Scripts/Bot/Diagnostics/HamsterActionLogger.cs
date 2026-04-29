using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Пишет единообразные runtime-логи выполнения bot action.
    /// </summary>
    internal static class HamsterActionLogger
    {
        public static void LogFire(PlannedAction action, float obstacleLeftX, string extra = null)
        {
            DebugManager.DiagLog(
                $"[Bot EXEC] FIRE kind={action.Kind} " +
                $"triggerX={action.TriggerX:F2} renderX={action.RenderWorldX:F2} obstacleLeftX={obstacleLeftX:F2} " +
                $"{extra ?? string.Empty}" +
                $"desc={action.Description}");
        }

        public static void LogComplete(PlannedAction action, HamsterStateEnum state)
        {
            DebugManager.DiagLog(
                $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                $"state={state} " +
                $"desc={action.Description}");
        }

        public static void LogCancel(PlannedAction action, string extra)
        {
            DebugManager.DiagLog(
                $"[Bot EXEC] CANCEL kind={action.Kind} " +
                $"{extra} " +
                $"desc={action.Description}");
        }

        public static void LogComplete(PlannedAction action, bool isBottomLine)
        {
            DebugManager.DiagLog(
                $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                $"lane={(isBottomLine ? "bottom" : "top")} " +
                $"desc={action.Description}");
        }
    }
}
