using System;
using System.Linq;
using Assets.Scripts.System;

namespace GameManagement.Progress
{
    public sealed class DefaultUnlockPolicy : IUnlockPolicy
    {
        public const int NextPartStarRequirement = 10;

        private readonly HierarchicalLevelCatalog _catalog;
        private readonly int _starUnlockOffset;

        public DefaultUnlockPolicy(HierarchicalLevelCatalog catalog, int starUnlockOffset)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _starUnlockOffset = starUnlockOffset;
        }

        public bool CanUnlockNextLevel(LevelProgressSnapshot snapshot, LevelProgressKey currentLevel, LevelProgressKey nextLevel)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolvePart(currentLevel, out var currentLocationIndex, out var currentPartIndex, out var currentPart) ||
                !TryResolvePart(nextLevel, out var nextLocationIndex, out var nextPartIndex, out _))
            {
                return false;
            }

            if (currentLocationIndex != nextLocationIndex)
            {
                return false;
            }

            if (currentPartIndex == nextPartIndex)
            {
                return true;
            }

            if (nextPartIndex != currentPartIndex + 1)
            {
                return false;
            }

            return MeetsNextPartUnlockRequirement(snapshot, currentLevel, currentPart);
        }

        public bool CanUnlockNextLocation(LevelProgressSnapshot snapshot, string currentLocationId, string nextLocationId)
        {
            var requiredStars = GetRequiredStarsForNextLocation(snapshot, currentLocationId);
            return requiredStars <= 0;
        }

        public int GetRequiredStarsForNextLocation(LevelProgressSnapshot snapshot, string currentLocationId)
        {
            if (_catalog == null || !_catalog.TryResolveLocationId(currentLocationId, out var locationIndex))
            {
                return int.MaxValue;
            }

            var requiredStars = CalculateMaxStarsForLocation(locationIndex) - _starUnlockOffset;
            if (requiredStars <= 0)
            {
                return 0;
            }

            var currentStars = snapshot.EnumerateLocation(currentLocationId).Sum(entry => entry.Stars);
            return Math.Max(requiredStars - currentStars, 0);
        }

        private int CalculateMaxStarsForLocation(int locationIndex)
        {
            if (!_catalog.TryGetLocation(locationIndex, out var location))
            {
                return 0;
            }

            var parts = location.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();
            var levelCount = parts.Sum(part => part.Levels?.Count ?? 0);
            return levelCount * LevelProgressEntry.MaxStars;
        }

        private bool TryResolvePart(
            LevelProgressKey key,
            out int locationIndex,
            out int partIndex,
            out HierarchicalLevelCatalog.PartOfDayEntry part)
        {
            locationIndex = -1;
            partIndex = -1;
            part = default;

            return _catalog.TryResolveLocationId(key.LocationId, out locationIndex) &&
                   _catalog.TryGetPart(locationIndex, key.PartOfDayId, out partIndex, out part);
        }

        private static bool MeetsNextPartUnlockRequirement(
            LevelProgressSnapshot snapshot,
            LevelProgressKey currentLevel,
            HierarchicalLevelCatalog.PartOfDayEntry currentPart)
        {
            var entries = snapshot
                .EnumeratePart(currentLevel.LocationId, currentLevel.PartOfDayId)
                .ToList();
            var requiredLevelCount = currentPart.Levels?.Count ?? 0;

            return requiredLevelCount > 0 &&
                   entries.Count >= requiredLevelCount &&
                   entries.All(entry => entry.Stars > 0) &&
                   entries.Sum(entry => entry.Stars) >= NextPartStarRequirement;
        }
    }
}
