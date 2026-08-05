using Assets.Scripts.System;

namespace GameManagement.Progress
{
    /// <summary>
    /// Представляет состояние прогресса игрового уровня.
    /// </summary>
    public sealed class LevelProgress
    {
        internal LevelProgress(
            HierarchicalLevelCatalog.LevelDescriptor descriptor,
            LevelProgressKey key,
            int levelNumber,
            bool isUnlocked,
            int stars)
        {
            Key = key;
            LocationIndex = descriptor.LocationIndex;
            PartIndex = descriptor.PartIndex;
            LevelKey = descriptor.LevelKey;
            Address = descriptor.Address;
            DisplayOrder = descriptor.DisplayOrder;
            LevelNumber = levelNumber;
            IsUnlocked = isUnlocked;
            Stars = stars;
        }

        /// <summary>
        /// Возвращает ключ сохранённого прогресса уровня.
        /// </summary>
        public LevelProgressKey Key { get; }

        /// <summary>
        /// Возвращает идентификатор родительской локации.
        /// </summary>
        public string LocationId => Key.LocationId;

        /// <summary>
        /// Возвращает индекс родительской локации в каталоге.
        /// </summary>
        public int LocationIndex { get; }

        /// <summary>
        /// Возвращает идентификатор родительской части суток.
        /// </summary>
        public string PartOfDayId => Key.PartOfDayId;

        /// <summary>
        /// Возвращает индекс родительской части суток в каталоге.
        /// </summary>
        public int PartIndex { get; }

        /// <summary>
        /// Возвращает индекс уровня внутри части суток.
        /// </summary>
        public int LevelIndex => Key.LevelIndex;

        /// <summary>
        /// Возвращает короткий ключ игрового уровня.
        /// </summary>
        public string LevelKey { get; }

        /// <summary>
        /// Возвращает полный адрес игрового уровня.
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// Возвращает отображаемый порядковый номер уровня внутри части суток.
        /// </summary>
        public int DisplayOrder { get; }

        /// <summary>
        /// Возвращает сквозной номер игрового уровня в порядке каталога.
        /// </summary>
        public int LevelNumber { get; }

        /// <summary>
        /// Показывает, открыт ли игровой уровень.
        /// </summary>
        public bool IsUnlocked { get; }

        /// <summary>
        /// Показывает, пройден ли игровой уровень минимум на одну звезду.
        /// </summary>
        public bool IsCompleted => Stars > 0;

        /// <summary>
        /// Показывает, пройден ли игровой уровень на три звезды.
        /// </summary>
        public bool IsMastered => Stars >= LevelProgressEntry.MaxStars;

        /// <summary>
        /// Возвращает количество полученных звёзд.
        /// </summary>
        public int Stars { get; }
    }
}
