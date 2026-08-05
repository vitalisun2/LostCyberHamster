using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;

namespace GameManagement.Progress
{
    /// <summary>
    /// Обновляет сохранённый прогресс уровней и предоставляет его агрегированное состояние для чтения.
    /// </summary>
    public sealed class ProgressService
    {
        private readonly HierarchicalLevelCatalog _catalog;
        private readonly IUnlockPolicy _unlockPolicy;
        private LevelProgressSnapshot _overviewSnapshot;
        private LevelProgressOverview _overview = LevelProgressOverview.Empty;

        /// <summary>
        /// Создаёт сервис прогресса для заданного каталога и правил открытия уровней.
        /// </summary>
        public ProgressService(HierarchicalLevelCatalog catalog, IUnlockPolicy unlockPolicy)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _unlockPolicy = unlockPolicy ?? throw new ArgumentNullException(nameof(unlockPolicy));
        }

        /// <summary>
        /// Применяет результат прохождения и открывает следующий доступный уровень или локацию.
        /// </summary>
        public LevelProgressSnapshot HandleLevelCompleted(LevelProgressSnapshot snapshot, LevelProgressKey progressKey, int stars)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // Обновляет лучший результат завершённого уровня.
            var clampedStars = ClampStars(stars);

            if (!snapshot.TryGet(progressKey, out var entry))
            {
                entry = new LevelProgressEntry(progressKey, true, 0);
            }

            snapshot = snapshot.Set(entry.ApplyStars(clampedStars));

            // Применяет правила открытия к следующему элементу каталога.
            if (TryGetNextProgressKey(progressKey, out var nextKey))
            {
                var shouldUnlock = string.Equals(progressKey.LocationId, nextKey.LocationId, StringComparison.OrdinalIgnoreCase)
                    ? _unlockPolicy.CanUnlockNextLevel(snapshot, progressKey, nextKey)
                    : _unlockPolicy.CanUnlockNextLocation(snapshot, progressKey.LocationId, nextKey.LocationId);

                if (shouldUnlock && (!snapshot.TryGet(nextKey, out var nextEntry) || !nextEntry.IsUnlocked))
                {
                    var unlocked = new LevelProgressEntry(nextKey, true, nextEntry?.Stars ?? 0);
                    snapshot = snapshot.Set(unlocked);
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Возвращает агрегированное состояние прогресса, повторно используя результат для того же LevelProgressSnapshot.
        /// </summary>
        public LevelProgressOverview GetOverview(LevelProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // Возвращает кэш для того же неизменяемого снимка.
            if (ReferenceEquals(snapshot, _overviewSnapshot))
            {
                return _overview;
            }

            // Перестраивает модель чтения после замены снимка прогресса.
            _overview = LevelProgressOverview.Create(_catalog, snapshot);
            _overviewSnapshot = snapshot;
            return _overview;
        }

        /// <summary>
        /// Возвращает количество звёзд, необходимых для открытия следующей локации.
        /// </summary>
        public int GetStarsToOpenNextLocation(LevelProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // Определяет текущую открытую границу каталога.
            var openedCount = GetContiguousUnlockedLocationCount(GetOverview(snapshot));
            if (openedCount == 0 || openedCount >= _catalog.LocationCount)
            {
                return 0;
            }

            // Запрашивает требование правила открытия для текущей локации.
            var currentLocationId = _catalog.GetLocationId(openedCount - 1);
            return _unlockPolicy.GetRequiredStarsForNextLocation(snapshot, currentLocationId);
        }

        /// <summary>
        /// Создаёт начальное состояние прогресса из текущего каталога.
        /// </summary>
        public LevelProgressSnapshot ResetProgress()
        {
            return LevelProgressSnapshot.CreateFromCatalog(_catalog);
        }

        /// <summary>
        /// Пытается найти следующий ключ прогресса в порядке каталога.
        /// </summary>
        public bool TryGetNextProgressKey(LevelProgressKey current, out LevelProgressKey next)
        {
            return InternalTryGetNextProgressKey(current, out next);
        }

        private bool InternalTryGetNextProgressKey(LevelProgressKey current, out LevelProgressKey next)
        {
            next = default;

            if (!_catalog.TryResolveLocationId(current.LocationId, out var locationIndex))
            {
                return false;
            }

            if (!_catalog.TryGetPart(locationIndex, current.PartOfDayId, out var partIndex, out var partEntry))
            {
                return false;
            }

            // Ищет следующий уровень в текущей части суток.
            var orderedLevels = partEntry.Levels?.OrderBy(level => level.Order).ToList()
                                ?? new List<HierarchicalLevelCatalog.LevelEntry>();
            var nextLevelIndex = current.LevelIndex + 1;
            if (nextLevelIndex < orderedLevels.Count)
            {
                next = new LevelProgressKey(
                    current.LocationId,
                    _catalog.GetPartId(locationIndex, partIndex),
                    nextLevelIndex);
                return true;
            }

            // Ищет первую непустую часть суток в текущей локации.
            var location = _catalog.Locations[locationIndex];
            for (int i = partIndex + 1; i < location.PartsOfDay.Count; i++)
            {
                var candidate = location.PartsOfDay[i];
                var candidateLevels = candidate.Levels?.OrderBy(level => level.Order).ToList()
                                      ?? new List<HierarchicalLevelCatalog.LevelEntry>();
                if (candidateLevels.Count == 0)
                {
                    continue;
                }

                var partId = _catalog.GetPartId(locationIndex, i);
                next = new LevelProgressKey(current.LocationId, partId, 0);
                return true;
            }

            // Ищет первый уровень следующих локаций.
            for (int nextLocationIndex = locationIndex + 1; nextLocationIndex < _catalog.LocationCount; nextLocationIndex++)
            {
                var locationEntry = _catalog.Locations[nextLocationIndex];
                var parts = locationEntry.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();

                for (int i = 0; i < parts.Count; i++)
                {
                    var candidate = parts[i];
                    var candidateLevels = candidate.Levels?.OrderBy(level => level.Order).ToList()
                                          ?? new List<HierarchicalLevelCatalog.LevelEntry>();
                    if (candidateLevels.Count == 0)
                    {
                        continue;
                    }

                    var locationId = _catalog.GetLocationId(nextLocationIndex);
                    var partId = _catalog.GetPartId(nextLocationIndex, i);
                    next = new LevelProgressKey(locationId, partId, 0);
                    return true;
                }
            }

            return false;
        }

        private static int ClampStars(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > LevelProgressEntry.MaxStars)
            {
                return LevelProgressEntry.MaxStars;
            }

            return value;
        }

        private static int GetContiguousUnlockedLocationCount(LevelProgressOverview overview)
        {
            int count = 0;

            foreach (var location in overview.Locations)
            {
                var isUnlocked = location.IsUnlocked;

                if (!isUnlocked)
                {
                    break;
                }

                count++;
            }

            return count;
        }
    }
}
