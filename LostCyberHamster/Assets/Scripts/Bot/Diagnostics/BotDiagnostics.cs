namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Центральный gate для диагностических логов бота.
    /// DebugManager остается transport/sink, а этот класс решает, какие bot-события писать.
    /// </summary>
    internal static class BotDiagnostics
    {
        private static BotDiagnosticLevel _maxLevel = BotDiagnosticLevel.Essential;
        private static BotDiagnosticCategory _enabledCategories = BotDiagnosticCategory.All;

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
        /// Возвращает диагностику в обычный режим: essential events по всем категориям.
        /// </summary>
        public static void Reset()
        {
            _maxLevel = BotDiagnosticLevel.Essential;
            _enabledCategories = BotDiagnosticCategory.All;
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
