/// <summary>
/// Уровень детализации логов бота.
/// </summary>
public enum BotLogLevel
{
    /// <summary>SELECT + EXECUTE + RESULT + DAMAGE</summary>
    Normal,
    /// <summary>Normal + SNAPSHOT + CLASSIFY + GENERATE</summary>
    Verbose
}

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Статический логгер BotV2. Пишет через DebugManager.DiagLog() в EditorLogs/diagnostic_log.txt.
    /// </summary>
    public static class BotLogger
    {
        public static BotLogLevel Level = BotLogLevel.Normal;

        public static void Log(BotLogLevel level, string message)
        {
            if (level == BotLogLevel.Verbose && Level == BotLogLevel.Normal) return;
            DebugManager.DiagLog(message);
        }
    }
}
