using System;
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
}
