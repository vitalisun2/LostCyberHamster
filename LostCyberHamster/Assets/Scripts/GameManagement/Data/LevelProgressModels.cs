using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using Vues.GameCore;

#nullable enable

namespace GameManagement.Progress
{
    [Serializable]
    public readonly struct LevelProgressKey : IEquatable<LevelProgressKey>
    {
        public LevelProgressKey(string locationId, string partOfDayId, int levelIndex)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                throw new ArgumentException("Location identifier must be provided", nameof(locationId));
            }

            if (string.IsNullOrWhiteSpace(partOfDayId))
            {
                throw new ArgumentException("Part-of-day identifier must be provided", nameof(partOfDayId));
            }

            if (levelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex), "Level index must be non-negative.");
            }

            LocationId = locationId.Trim();
            PartOfDayId = partOfDayId.Trim();
            LevelIndex = levelIndex;
        }

        public string LocationId { get; }
        public string PartOfDayId { get; }
        public int LevelIndex { get; }

        public bool Equals(LevelProgressKey other)
        {
            return string.Equals(LocationId, other.LocationId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(PartOfDayId, other.PartOfDayId, StringComparison.OrdinalIgnoreCase)
                   && LevelIndex == other.LevelIndex;
        }

        public override bool Equals(object? obj)
        {
            return obj is LevelProgressKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var locationHash = StringComparer.OrdinalIgnoreCase.GetHashCode(LocationId);
            var partHash = StringComparer.OrdinalIgnoreCase.GetHashCode(PartOfDayId);
            return HashCode.Combine(locationHash, partHash, LevelIndex);
        }

        public override string ToString()
        {
            return $"{LocationId}:{PartOfDayId}:{LevelIndex}";
        }

        public bool BelongsToLocation(string locationId)
        {
            return !string.IsNullOrWhiteSpace(locationId)
                   && string.Equals(LocationId, locationId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool BelongsToPart(string locationId, string partOfDayId)
        {
            return BelongsToLocation(locationId)
                   && !string.IsNullOrWhiteSpace(partOfDayId)
                   && string.Equals(PartOfDayId, partOfDayId.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class LevelProgressEntry
    {
        public const int MaxStars = 3;

        public LevelProgressEntry(LevelProgressKey key)
            : this(key, false, 0)
        {
        }

        public LevelProgressEntry(LevelProgressKey key, bool isUnlocked, int stars)
        {
            Key = key;
            Stars = NormalizeStars(stars);
            IsUnlocked = isUnlocked || Stars > 0;
        }

        public LevelProgressKey Key { get; }
        public bool IsUnlocked { get; }
        public int Stars { get; }
        public bool IsCompleted => Stars > 0;

        public LevelProgressEntry Unlock()
        {
            if (IsUnlocked)
            {
                return this;
            }

            return new LevelProgressEntry(Key, true, Stars);
        }

        public LevelProgressEntry WithStars(int stars)
        {
            var normalized = NormalizeStars(stars);
            if (normalized == Stars)
            {
                return this;
            }

            return new LevelProgressEntry(Key, IsUnlocked || normalized > 0, normalized);
        }

        public LevelProgressEntry ApplyStars(int stars)
        {
            var normalized = NormalizeStars(stars);
            if (normalized <= Stars)
            {
                return Unlock();
            }

            return new LevelProgressEntry(Key, true, normalized);
        }

        private static int NormalizeStars(int stars)
        {
            if (stars < 0)
            {
                return 0;
            }

            if (stars > MaxStars)
            {
                return MaxStars;
            }

            return stars;
        }
    }

    public sealed class LevelProgressSnapshot
    {
        private readonly Dictionary<LevelProgressKey, LevelProgressEntry> _entries;

        private LevelProgressSnapshot(Dictionary<LevelProgressKey, LevelProgressEntry> entries)
        {
            _entries = entries;
        }

        public LevelProgressSnapshot(IEnumerable<LevelProgressEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _entries = entries.ToDictionary(entry => entry.Key, entry => entry);
        }

        public static LevelProgressSnapshot Empty { get; } = new LevelProgressSnapshot(new Dictionary<LevelProgressKey, LevelProgressEntry>());

        public IReadOnlyCollection<LevelProgressEntry> Entries => _entries.Values;

        public bool TryGet(LevelProgressKey key, out LevelProgressEntry entry)
        {
            return _entries.TryGetValue(key, out entry!);
        }

        public LevelProgressSnapshot Ensure(LevelProgressKey key)
        {
            if (_entries.ContainsKey(key))
            {
                return this;
            }

            var clone = CloneInternal();
            clone[key] = new LevelProgressEntry(key);
            return new LevelProgressSnapshot(clone);
        }

        public LevelProgressSnapshot Set(LevelProgressEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var clone = CloneInternal();
            clone[entry.Key] = entry;
            return new LevelProgressSnapshot(clone);
        }

        public IEnumerable<LevelProgressEntry> EnumerateLocation(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return Enumerable.Empty<LevelProgressEntry>();
            }

            return _entries.Values
                .Where(entry => entry.Key.BelongsToLocation(locationId))
                .OrderBy(entry => entry.Key.PartOfDayId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Key.LevelIndex)
                .ToList();
        }

        public IEnumerable<LevelProgressEntry> EnumeratePart(string locationId, string partOfDayId)
        {
            if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(partOfDayId))
            {
                return Enumerable.Empty<LevelProgressEntry>();
            }

            return _entries.Values
                .Where(entry => entry.Key.BelongsToPart(locationId, partOfDayId))
                .OrderBy(entry => entry.Key.LevelIndex)
                .ToList();
        }

        public bool IsLevelUnlocked(LevelProgressKey key)
        {
            return _entries.TryGetValue(key, out var entry) && entry.IsUnlocked;
        }

        public int GetStars(LevelProgressKey key)
        {
            return _entries.TryGetValue(key, out var entry) ? entry.Stars : 0;
        }

        private Dictionary<LevelProgressKey, LevelProgressEntry> CloneInternal()
        {
            return new Dictionary<LevelProgressKey, LevelProgressEntry>(_entries);
        }

        public static LevelProgressSnapshot CreateFromCatalog(HierarchicalLevelCatalog catalog, bool unlockFirstLevel = true)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var entries = new Dictionary<LevelProgressKey, LevelProgressEntry>();
            bool firstUnlockedAssigned = !unlockFirstLevel;

            for (int locationIndex = 0; locationIndex < catalog.Locations.Count; locationIndex++)
            {
                var locationEntry = catalog.Locations[locationIndex];
                var locationId = ResolveLocationId(locationEntry.Key, locationIndex);
                var parts = locationEntry.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();
                int partIndex = 0;

                foreach (var part in parts)
                {
                    var partId = ResolvePartId(part.Key, partIndex);
                    var orderedLevels = part.Levels?.OrderBy(level => level.Order).ToList() ?? new List<HierarchicalLevelCatalog.LevelEntry>();

                    for (int levelIndex = 0; levelIndex < orderedLevels.Count; levelIndex++)
                    {
                        var key = new LevelProgressKey(locationId, partId, levelIndex);
                        var isUnlocked = !firstUnlockedAssigned;
                        if (isUnlocked)
                        {
                            firstUnlockedAssigned = true;
                        }

                        entries[key] = new LevelProgressEntry(key, isUnlocked, 0);
                    }

                    partIndex++;
                }
            }

            return entries.Count == 0
                ? Empty
                : new LevelProgressSnapshot(entries);
        }

        private static string ResolveLocationId(string? locationKey, int locationIndex)
        {
            if (!string.IsNullOrWhiteSpace(locationKey))
            {
                return locationKey.Trim();
            }

            return $"location_{locationIndex:D2}";
        }

        private static string ResolvePartId(string? partKey, int partIndex)
        {
            if (!string.IsNullOrWhiteSpace(partKey))
            {
                return partKey.Trim();
            }

            return ((PartOfDayEnum)(partIndex + 1)).ToString();
        }
    }
}
