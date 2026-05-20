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
            return;
        }

        public static void LogComplete(PlannedAction action, HamsterStateEnum state)
        {
            return;
        }

        public static void LogCancel(PlannedAction action, string extra)
        {
            return;
        }

        public static void LogComplete(PlannedAction action, bool isBottomLine)
        {
            return;
        }
    }
}
