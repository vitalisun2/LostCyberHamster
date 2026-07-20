using System;
using System.Linq;
using Assets.Scripts.System;

namespace GameManagement.Progress
{
    public sealed class DefaultUnlockPolicy : IUnlockPolicy
    {
        private readonly HierarchicalLevelCatalog _catalog;
        private readonly int _starUnlockOffset;

        public DefaultUnlockPolicy(HierarchicalLevelCatalog catalog, int starUnlockOffset)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _starUnlockOffset = starUnlockOffset;
        }

        public bool CanUnlockNextLevel(LevelProgressSnapshot snapshot, LevelProgressKey currentLevel, LevelProgressKey nextLevel)
        {
            return true;
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
    }
}
