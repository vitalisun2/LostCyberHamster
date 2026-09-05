using System;
using System.Globalization;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Определяет момент обновления набора дневных квестов.
    /// </summary>
    public sealed class DailyQuestScheduler
    {
        private const string GenerationDateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Проверяет необходимость первой генерации или обновления
        /// в новый локальный день.
        /// </summary>
        public bool ShouldGenerate(
            DailyQuestSetState state,
            DateTime localNow)
        {
            string date = GetGenerationDate(localNow);
            return state == null ||
                   !string.Equals(state.GenerationDate, date, StringComparison.Ordinal) &&
                   !(state.UsedGenerationDates?.Contains(date) ?? false);
        }

        /// <summary>
        /// Возвращает локальную дату генерации в стабильном формате.
        /// </summary>
        public string GetGenerationDate(DateTime localNow)
        {
            return localNow.Date.ToString(
                GenerationDateFormat,
                CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Возвращает начало следующего локального дня.
        /// </summary>
        public DateTime GetNextGenerationTime(DateTime localNow)
        {
            return localNow.Date.AddDays(1);
        }
    }
}
