using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Assets.Scripts.System;

#nullable enable

namespace GameManagement.Progress
{
    /// <summary>
    /// Представляет готовое для чтения состояние прогресса локаций, частей суток и игровых уровней.
    /// </summary>
    public sealed class LevelProgressOverview
    {
        private static readonly IReadOnlyList<LocationProgress> EmptyLocations =
            Array.Empty<LocationProgress>();
        private static readonly IReadOnlyList<LevelProgress> EmptyLevels =
            Array.Empty<LevelProgress>();

        private readonly IReadOnlyDictionary<string, LocationProgress> _locationsById;
        private readonly IReadOnlyDictionary<LevelProgressKey, LevelProgress> _levelsByKey;

        private LevelProgressOverview(
            IReadOnlyList<LocationProgress> locations,
            IReadOnlyList<LevelProgress> levels,
            IReadOnlyDictionary<string, LocationProgress> locationsById,
            IReadOnlyDictionary<LevelProgressKey, LevelProgress> levelsByKey)
        {
            Locations = locations;
            Levels = levels;
            _locationsById = locationsById;
            _levelsByKey = levelsByKey;
        }

        /// <summary>
        /// Возвращает пустое состояние прогресса.
        /// </summary>
        public static LevelProgressOverview Empty { get; } = new LevelProgressOverview(
            EmptyLocations,
            EmptyLevels,
            new ReadOnlyDictionary<string, LocationProgress>(
                new Dictionary<string, LocationProgress>(StringComparer.OrdinalIgnoreCase)),
            new ReadOnlyDictionary<LevelProgressKey, LevelProgress>(
                new Dictionary<LevelProgressKey, LevelProgress>()));

        /// <summary>
        /// Возвращает локации в порядке каталога.
        /// </summary>
        public IReadOnlyList<LocationProgress> Locations { get; }

        /// <summary>
        /// Возвращает игровые уровни в порядке каталога.
        /// </summary>
        public IReadOnlyList<LevelProgress> Levels { get; }

        /// <summary>
        /// Пытается получить состояние локации по её идентификатору.
        /// </summary>
        public bool TryGetLocation(string locationId, out LocationProgress location)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                location = null!;
                return false;
            }

            return _locationsById.TryGetValue(locationId.Trim(), out location!);
        }

        /// <summary>
        /// Пытается получить состояние части суток по идентификаторам локации и части.
        /// </summary>
        public bool TryGetPart(string locationId, string partOfDayId, out PartProgress part)
        {
            if (TryGetLocation(locationId, out var location))
            {
                return location.TryGetPart(partOfDayId, out part);
            }

            part = null!;
            return false;
        }

        /// <summary>
        /// Пытается получить состояние игрового уровня по ключу прогресса.
        /// </summary>
        public bool TryGetLevel(LevelProgressKey key, out LevelProgress level)
        {
            return _levelsByKey.TryGetValue(key, out level!);
        }

        internal static LevelProgressOverview Create(
            HierarchicalLevelCatalog catalog,
            LevelProgressSnapshot snapshot)
        {
            if (catalog.IsEmpty)
            {
                return Empty;
            }

            var locations = new List<LocationProgress>(catalog.LocationCount);
            var levels = new List<LevelProgress>();
            var locationsById = new Dictionary<string, LocationProgress>(StringComparer.OrdinalIgnoreCase);
            var levelsByKey = new Dictionary<LevelProgressKey, LevelProgress>();
            var previousLocationIsUnlocked = true;

            // Собирает игровые уровни и агрегаты частей суток одним проходом по каталогу.
            for (int locationIndex = 0; locationIndex < catalog.LocationCount; locationIndex++)
            {
                var locationEntry = catalog.Locations[locationIndex];
                var locationId = catalog.GetLocationId(locationIndex);
                var parts = new List<PartProgress>(locationEntry.PartsOfDay.Count);
                var locationTotalStars = 0;
                var locationTotalLevels = 0;
                var locationCompletedLevels = 0;
                var locationMasteredLevels = 0;
                var locationHasUnlockedLevel = false;

                for (int partIndex = 0; partIndex < locationEntry.PartsOfDay.Count; partIndex++)
                {
                    var partId = catalog.GetPartId(locationIndex, partIndex);
                    var partLevels = new List<LevelProgress>();
                    var partTotalStars = 0;
                    var partCompletedLevels = 0;
                    var partMasteredLevels = 0;
                    var partIsUnlocked = false;

                    foreach (var levelEntry in locationEntry.PartsOfDay[partIndex].Levels)
                    {
                        if (!catalog.TryFindLevel(levelEntry.Address, out var descriptor)
                            || !HierarchicalLevelCatalog.IsGameplayLevelKey(descriptor.LevelKey))
                        {
                            continue;
                        }

                        var key = new LevelProgressKey(locationId, partId, descriptor.LevelIndex);
                        var isUnlocked = snapshot.TryGet(key, out var entry) && entry.IsUnlocked;
                        var stars = entry?.Stars ?? 0;
                        var level = new LevelProgress(
                            descriptor,
                            key,
                            levels.Count + 1,
                            isUnlocked,
                            stars);

                        partLevels.Add(level);
                        levels.Add(level);
                        levelsByKey[key] = level;

                        partIsUnlocked |= level.IsUnlocked;
                        partTotalStars += level.Stars;
                        partCompletedLevels += level.IsCompleted ? 1 : 0;
                        partMasteredLevels += level.IsMastered ? 1 : 0;
                    }

                    var part = new PartProgress(
                        locationId,
                        locationIndex,
                        partId,
                        partIndex,
                        partLevels,
                        partIsUnlocked,
                        partTotalStars,
                        partCompletedLevels,
                        partMasteredLevels);

                    parts.Add(part);
                    locationHasUnlockedLevel |= part.IsUnlocked;
                    locationTotalStars += part.TotalStars;
                    locationTotalLevels += part.TotalLevels;
                    locationCompletedLevels += part.CompletedLevels;
                    locationMasteredLevels += part.MasteredLevels;
                }

                // Сохраняет непрерывную доступность локаций от начала каталога.
                var locationIsUnlocked = locationIndex == 0 ||
                                         (previousLocationIsUnlocked &&
                                          locationHasUnlockedLevel);
                previousLocationIsUnlocked = locationIsUnlocked;

                // Фиксирует итоговое состояние локации и быстрые индексы чтения.
                var location = new LocationProgress(
                    locationId,
                    locationIndex,
                    parts,
                    locationIsUnlocked,
                    locationTotalStars,
                    locationTotalLevels,
                    locationCompletedLevels,
                    locationMasteredLevels);

                locations.Add(location);
                locationsById[locationId] = location;
            }

            return new LevelProgressOverview(
                new ReadOnlyCollection<LocationProgress>(locations),
                new ReadOnlyCollection<LevelProgress>(levels),
                new ReadOnlyDictionary<string, LocationProgress>(locationsById),
                new ReadOnlyDictionary<LevelProgressKey, LevelProgress>(levelsByKey));
        }

    }
}
