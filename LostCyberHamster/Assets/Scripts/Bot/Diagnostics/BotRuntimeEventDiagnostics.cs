using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Диагностика runtime-событий уровня: результат теста, экономика энергии и финальное состояние.
    /// </summary>
    internal static class BotRuntimeEventDiagnostics
    {
        public static void LogEnergyStart(int value)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Economy))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Economy,
                BotDiagnosticLevel.Essential,
                $"[Energy] start value={value}",
                DebugManager.DiagChannel.Economy);
        }

        public static void LogEnergyChanged(int delta, int value)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Economy))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Economy,
                BotDiagnosticLevel.Essential,
                $"[Energy] change delta={delta:+#;-#;0} value={value}",
                DebugManager.DiagChannel.Economy);
        }

        public static void LogEnergyAdded(int amount, int value)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Economy))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Economy,
                BotDiagnosticLevel.Essential,
                $"[Energy] added amount={amount} value={value}",
                DebugManager.DiagChannel.Economy);
        }

        public static void LogEnergySpent(int amount, int value)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Economy))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Economy,
                BotDiagnosticLevel.Essential,
                $"[Energy] spent amount={amount} value={value}",
                DebugManager.DiagChannel.Economy);
        }

        public static void LogTestFinish(GameManager gameManager, Hamster hamster)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.TestResult))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.TestResult,
                BotDiagnosticLevel.Essential,
                $"[TEST FINISH] state={gameManager.State} " +
                $"lives={hamster.Lives.Value} energy={hamster.Energy.Value}");
        }

        public static void LogLevelCompleted(int levelId, int stars)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.TestResult))
                return;

            string message = $"[TEST RESULT] WIN level={levelId} stars={stars}";
            BotDiagnostics.Log(BotDiagnosticCategory.TestResult, BotDiagnosticLevel.Essential, message);
            BotDiagnostics.Log(
                BotDiagnosticCategory.TestResult,
                BotDiagnosticLevel.Essential,
                message,
                DebugManager.DiagChannel.Stability);
        }

        public static void LogLevelFailed()
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.TestResult))
                return;

            const string message = "[TEST RESULT] FAIL";
            BotDiagnostics.Log(BotDiagnosticCategory.TestResult, BotDiagnosticLevel.Essential, message);
            BotDiagnostics.Log(
                BotDiagnosticCategory.TestResult,
                BotDiagnosticLevel.Essential,
                message,
                DebugManager.DiagChannel.Stability);
        }
    }
}
