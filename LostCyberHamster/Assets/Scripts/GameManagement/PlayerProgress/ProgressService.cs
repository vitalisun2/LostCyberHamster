using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;

namespace GameManagement.Progress
{
    public sealed class ProgressService
    {
        private readonly HierarchicalLevelCatalog _catalog;
        private readonly IUnlockPolicy _unlockPolicy;

        public ProgressService(HierarchicalLevelCatalog catalog, IUnlockPolicy unlockPolicy)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _unlockPolicy = unlockPolicy ?? throw new ArgumentNullException(nameof(unlockPolicy));
        }

        public LevelProgressSnapshot HandleLevelCompleted(LevelProgressSnapshot snapshot, LevelProgressKey progressKey, int stars)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var clampedStars = ClampStars(stars);

            if (!snapshot.TryGet(progressKey, out var entry))
            {
                entry = new LevelProgressEntry(progressKey, true, 0);
            }

            snapshot = snapshot.Set(entry.ApplyStars(clampedStars));

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

        public IReadOnlyList<LocationInfo> BuildOpenedLocations(LevelProgressSnapshot snapshot, LocationInfoList infoList)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var result = new List<LocationInfo>();
            var infos = infoList?.locations ?? Array.Empty<LocationInfo>();

            for (int index = 0; index < _catalog.LocationCount; index++)
            {
                var locationId = _catalog.GetLocationId(index);
                var entries = snapshot.EnumerateLocation(locationId);
                var isUnlocked = entries.Any(entry => entry.IsUnlocked);

                if (!isUnlocked && index == 0)
                {
                    isUnlocked = true;
                }

                if (!isUnlocked)
                {
                    break;
                }

                var info = index < infos.Length && infos[index] != null
                    ? infos[index]
                    : new LocationInfo
                    {
                        name = _catalog.Locations[index].Key ?? $"Location {index + 1}",
                        image = string.Empty,
                        sysname = _catalog.Locations[index].Key ?? string.Empty,
                        levels = Array.Empty<string>()
                    };

                result.Add(info);
            }

            return result;
        }

        public int GetStarsToOpenNextLocation(LevelProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var openedCount = GetContiguousUnlockedLocationCount(snapshot);
            if (openedCount == 0 || openedCount >= _catalog.LocationCount)
            {
                return 0;
            }

            var currentLocationId = _catalog.GetLocationId(openedCount - 1);
            return _unlockPolicy.GetRequiredStarsForNextLocation(snapshot, currentLocationId);
        }

        public LevelProgressSnapshot ResetProgress()
        {
            return LevelProgressSnapshot.CreateFromCatalog(_catalog);
        }

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

        private int GetContiguousUnlockedLocationCount(LevelProgressSnapshot snapshot)
        {
            int count = 0;

            for (int index = 0; index < _catalog.LocationCount; index++)
            {
                var locationId = _catalog.GetLocationId(index);
                var isUnlocked = snapshot.EnumerateLocation(locationId).Any(entry => entry.IsUnlocked);

                if (!isUnlocked)
                {
                    if (index == 0)
                    {
                        count++;
                    }

                    break;
                }

                count++;
            }

            return count;
        }
    }
}
