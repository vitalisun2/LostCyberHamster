using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Пишет единообразные runtime-логи выполнения bot action.
    /// </summary>
    internal static class HamsterActionLogger
    {
        public static void LogFire(PlannedAction action, float obstacleLeftX, string extra = null)
        {
            BotExecutionDiagnostics.LogFire(action, obstacleLeftX, extra);
        }

        public static void LogComplete(PlannedAction action, HamsterStateEnum state)
        {
            BotExecutionDiagnostics.LogComplete(action, state);
        }

        public static void LogCancel(PlannedAction action, string extra)
        {
            BotExecutionDiagnostics.LogCancel(action, extra);
        }

        public static void LogComplete(PlannedAction action, bool isBottomLine)
        {
            BotExecutionDiagnostics.LogComplete(action, isBottomLine);
        }
    }
}
