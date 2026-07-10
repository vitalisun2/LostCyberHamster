namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Центральный gate для диагностических логов бота.
    /// DebugManager остается transport/sink, а этот класс решает, какие bot-события писать.
    /// </summary>
    internal static class BotDiagnostics
    {
        private const BotDiagnosticCategory DefaultEssentialCategories =
            BotDiagnosticCategory.TestResult
            | BotDiagnosticCategory.RuntimeSafety
            | BotDiagnosticCategory.Replan;

        private static BotDiagnosticLevel _maxLevel = BotDiagnosticLevel.Essential;
        private static BotDiagnosticCategory _enabledCategories = DefaultEssentialCategories;

        /// <summary>
        /// Возвращает true, если указанная категория и уровень сейчас пишутся в diagnostic log.
        /// Вызов должен происходить до дорогого форматирования строк.
        /// </summary>
        public static bool IsEnabled(
            BotDiagnosticCategory category,
            BotDiagnosticLevel level = BotDiagnosticLevel.Essential)
        {
            return level <= _maxLevel
                   && category != BotDiagnosticCategory.None
                   && (_enabledCategories & category) != 0;
        }

        /// <summary>
        /// Задает максимальный уровень подробности bot diagnostics.
        /// </summary>
        public static void SetMaxLevel(BotDiagnosticLevel maxLevel)
        {
            _maxLevel = maxLevel;
        }

        /// <summary>
        /// Задает набор активных категорий bot diagnostics.
        /// </summary>
        public static void SetEnabledCategories(BotDiagnosticCategory enabledCategories)
        {
            _enabledCategories = enabledCategories;
        }

        /// <summary>
        /// Возвращает диагностику в обычный режим: essential events из набора по умолчанию.
        /// </summary>
        public static void Reset()
        {
            _maxLevel = BotDiagnosticLevel.Essential;
            _enabledCategories = DefaultEssentialCategories;
        }

        /// <summary>
        /// Пишет bot diagnostic message, если включены категория и уровень.
        /// </summary>
        public static void Log(
            BotDiagnosticCategory category,
            BotDiagnosticLevel level,
            string message,
            DebugManager.DiagChannel channel = DebugManager.DiagChannel.BotEvents)
        {
            if (!IsEnabled(category, level))
                return;

            DebugManager.DiagLog(message, channel);
        }
    }
}
