using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable enable

namespace GameManagement.Progress
{
    /// <summary>
    /// Представляет агрегированное состояние прогресса локации.
    /// </summary>
    public sealed class LocationProgress
    {
        private readonly IReadOnlyDictionary<string, PartProgress> _partsById;

        internal LocationProgress(
            string locationId,
            int locationIndex,
            List<PartProgress> parts,
            bool isUnlocked,
            int totalStars,
            int totalLevels,
            int completedLevels,
            int masteredLevels)
        {
            LocationId = locationId;
            LocationIndex = locationIndex;
            Parts = new ReadOnlyCollection<PartProgress>(parts);
            IsUnlocked = isUnlocked;
            TotalStars = totalStars;
            TotalLevels = totalLevels;
            CompletedLevels = completedLevels;
            MasteredLevels = masteredLevels;

            var partsById = new Dictionary<string, PartProgress>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in parts)
            {
                partsById[part.PartOfDayId] = part;
            }

            _partsById = new ReadOnlyDictionary<string, PartProgress>(partsById);
        }

        /// <summary>
        /// Возвращает идентификатор локации.
        /// </summary>
        public string LocationId { get; }

        /// <summary>
        /// Возвращает индекс локации в каталоге.
        /// </summary>
        public int LocationIndex { get; }

        /// <summary>
        /// Возвращает отображаемый порядковый номер локации.
        /// </summary>
        public int DisplayOrder => LocationIndex + 1;

        /// <summary>
        /// Возвращает части суток в порядке каталога.
        /// </summary>
        public IReadOnlyList<PartProgress> Parts { get; }

        /// <summary>
        /// Показывает, доступна ли локация в непрерывной последовательности каталога.
        /// </summary>
        public bool IsUnlocked { get; }

        /// <summary>
        /// Показывает, пройдены ли все игровые уровни локации минимум на одну звезду.
        /// </summary>
        public bool IsCompleted => TotalLevels > 0 && CompletedLevels == TotalLevels;

        /// <summary>
        /// Показывает, пройдены ли все игровые уровни локации на три звезды.
        /// </summary>
        public bool IsMastered => TotalLevels > 0 && MasteredLevels == TotalLevels;

        /// <summary>
        /// Возвращает сумму звёзд игровых уровней локации.
        /// </summary>
        public int TotalStars { get; }

        /// <summary>
        /// Возвращает количество игровых уровней локации.
        /// </summary>
        public int TotalLevels { get; }

        /// <summary>
        /// Возвращает количество пройденных игровых уровней локации.
        /// </summary>
        public int CompletedLevels { get; }

        /// <summary>
        /// Возвращает количество игровых уровней локации с тремя звёздами.
        /// </summary>
        public int MasteredLevels { get; }

        /// <summary>
        /// Пытается получить состояние части суток по её идентификатору.
        /// </summary>
        public bool TryGetPart(string partOfDayId, out PartProgress part)
        {
            if (string.IsNullOrWhiteSpace(partOfDayId))
            {
                part = null!;
                return false;
            }

            return _partsById.TryGetValue(partOfDayId.Trim(), out part!);
        }
    }
}
