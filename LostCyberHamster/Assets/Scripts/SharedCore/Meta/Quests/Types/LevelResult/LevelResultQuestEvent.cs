using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Сообщает результат успешно завершённого уровня.
    /// </summary>
    public sealed class LevelResultQuestEvent : QuestEvent
    {
        /// <summary>
        /// Последовательный номер завершённого уровня.
        /// </summary>
        public int LevelId { get; }

        /// <summary>
        /// Полученное количество звёзд.
        /// </summary>
        public int Stars { get; }

        /// <summary>
        /// Создаёт результат успешно завершённого уровня.
        /// </summary>
        public LevelResultQuestEvent(int levelId, int stars)
        {
            // Проверяем идентификатор завершённого уровня.
            if (levelId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelId),
                    levelId,
                    "Номер уровня должен быть положительным.");
            }

            // Проверяем допустимый результат победы.
            if (stars < 1 || stars > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stars),
                    stars,
                    "Количество звёзд должно быть от 1 до 3.");
            }

            LevelId = levelId;
            Stars = stars;
        }
    }
}
