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
            if (state == null)
            {
                return true;
            }

            if (!DateTime.TryParseExact(
                    state.GenerationDate,
                    GenerationDateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime generatedDay))
            {
                return true;
            }

            return generatedDay.Date < localNow.Date;
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
