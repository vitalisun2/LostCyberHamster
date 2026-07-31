using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Контентное описание одного квеста.
    /// </summary>
    [Serializable]
    public sealed class QuestDefinition
    {
        /// <summary>
        /// Стабильный идентификатор квеста.
        /// </summary>
        public string Id;

        /// <summary>
        /// Ключ локализованного названия квеста.
        /// </summary>
        public string TitleLocalizationKey;

        /// <summary>
        /// Раздел production-каталога.
        /// </summary>
        [NonSerialized]
        public QuestCategory Category;

        /// <summary>
        /// Широкий тип логики квеста.
        /// </summary>
        public QuestType Type;

        /// <summary>
        /// Действие, которое даёт прогресс счётчику.
        /// </summary>
        public string ActionId;

        /// <summary>
        /// Номер уровня для квеста результата уровня.
        /// </summary>
        public int RequiredLevelId;

        /// <summary>
        /// Минимальное количество звёзд для результата уровня.
        /// </summary>
        public int RequiredStars;

        /// <summary>
        /// Значение прогресса для завершения.
        /// </summary>
        public int TargetAmount;

        /// <summary>
        /// Ресурс награды за квест.
        /// </summary>
        public ResourceType RewardType;

        /// <summary>
        /// Количество ресурса в награде.
        /// </summary>
        public int RewardAmount;
    }
}
