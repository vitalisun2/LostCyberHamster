using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameManagement.Progress
{
    /// <summary>
    /// Представляет агрегированное состояние прогресса части суток.
    /// </summary>
    public sealed class PartProgress
    {
        internal PartProgress(
            string locationId,
            int locationIndex,
            string partOfDayId,
            int partIndex,
            List<LevelProgress> levels,
            bool isUnlocked,
            int totalStars,
            int completedLevels,
            int masteredLevels)
        {
            LocationId = locationId;
            LocationIndex = locationIndex;
            PartOfDayId = partOfDayId;
            PartIndex = partIndex;
            Levels = new ReadOnlyCollection<LevelProgress>(levels);
            IsUnlocked = isUnlocked;
            TotalStars = totalStars;
            CompletedLevels = completedLevels;
            MasteredLevels = masteredLevels;
        }

        /// <summary>
        /// Возвращает идентификатор родительской локации.
        /// </summary>
        public string LocationId { get; }

        /// <summary>
        /// Возвращает индекс родительской локации в каталоге.
        /// </summary>
        public int LocationIndex { get; }

        /// <summary>
        /// Возвращает идентификатор части суток.
        /// </summary>
        public string PartOfDayId { get; }

        /// <summary>
        /// Возвращает индекс части суток в локации.
        /// </summary>
        public int PartIndex { get; }

        /// <summary>
        /// Возвращает отображаемый порядковый номер части суток.
        /// </summary>
        public int DisplayOrder => PartIndex + 1;

        /// <summary>
        /// Возвращает игровые уровни в порядке каталога.
        /// </summary>
        public IReadOnlyList<LevelProgress> Levels { get; }

        /// <summary>
        /// Показывает, открыт ли хотя бы один игровой уровень части суток.
        /// </summary>
        public bool IsUnlocked { get; }

        /// <summary>
        /// Показывает, пройдены ли все игровые уровни части суток минимум на одну звезду.
        /// </summary>
        public bool IsCompleted => TotalLevels > 0 && CompletedLevels == TotalLevels;

        /// <summary>
        /// Показывает, пройдены ли все игровые уровни части суток на три звезды.
        /// </summary>
        public bool IsMastered => TotalLevels > 0 && MasteredLevels == TotalLevels;

        /// <summary>
        /// Возвращает сумму звёзд игровых уровней части суток.
        /// </summary>
        public int TotalStars { get; }

        /// <summary>
        /// Возвращает количество игровых уровней части суток.
        /// </summary>
        public int TotalLevels => Levels.Count;

        /// <summary>
        /// Возвращает количество пройденных игровых уровней части суток.
        /// </summary>
        public int CompletedLevels { get; }

        /// <summary>
        /// Возвращает количество игровых уровней части суток с тремя звёздами.
        /// </summary>
        public int MasteredLevels { get; }
    }
}
