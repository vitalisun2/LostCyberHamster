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
        /// Номер уровня для квеста результата уровня. Ноль означает любой уровень.
        /// </summary>
        public int RequiredLevelId;

        /// <summary>
        /// Идентификатор нужной локации. Пустое значение означает любую локацию.
        /// </summary>
        public string RequiredLocationId;

        /// <summary>
        /// Идентификатор нужной части суток. Пустое значение означает любую часть суток.
        /// </summary>
        public string RequiredPartOfDayId;

        /// <summary>
        /// Засчитывать каждый подходящий уровень только один раз.
        /// </summary>
        public bool CountUniqueLevels;

        /// <summary>
        /// Минимальное количество звёзд для результата уровня.
        /// </summary>
        public int RequiredStars;

        /// <summary>
        /// Идентификатор постоянного состояния игрока.
        /// </summary>
        public string StateId;

        /// <summary>
        /// Идентификатор сущности, состояние которой проверяет квест.
        /// </summary>
        public string EntityId;

        /// <summary>
        /// Значение состояния, достаточное для выполнения условия.
        /// </summary>
        public int RequiredValue;

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
