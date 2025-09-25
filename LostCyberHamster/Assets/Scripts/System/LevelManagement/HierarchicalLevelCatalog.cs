using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Placeholder implementation for the forthcoming hierarchical level model.
    /// </summary>
    public class HierarchicalLevelCatalog : ILevelCatalog
    {
        private readonly IReadOnlyList<LocationEntry> _locations;

        public HierarchicalLevelCatalog(IEnumerable<LocationEntry> locations)
        {
            _locations = locations?.ToList() ?? throw new ArgumentNullException(nameof(locations));
        }

        /// <summary>
        /// Hierarchical catalog supports variable level counts; legacy constant is not applicable.
        /// </summary>
        public int LevelsPerLocation => 0;

        public string GetLevelName(int levelNumber)
        {
            throw new NotSupportedException("Hierarchical catalog does not expose sequential level numbers.");
        }

        public string GetLevelName(int locationIndex, PartOfDayEnum partOfDay)
        {
            throw new NotSupportedException("Hierarchical catalog relies on explicit part-of-day identifiers.");
        }

        public int GetLevelNumber(int locationIndex, PartOfDayEnum partOfDay)
        {
            throw new NotSupportedException("Hierarchical catalog does not convert to sequential numbers.");
        }

        public IEnumerable<string> GetLevelsForLocation(int locationIndex)
        {
            if (locationIndex < 0 || locationIndex >= _locations.Count)
            {
                yield break;
            }

            foreach (var day in _locations[locationIndex].PartsOfDay)
            {
                foreach (var level in day.Levels.OrderBy(l => l.Order))
                {
                    yield return level.Address;
                }
            }
        }

        public IEnumerable<string> GetLevelsForPartOfDay(int locationIndex, string partOfDayKey)
        {
            if (locationIndex < 0 || locationIndex >= _locations.Count)
            {
                yield break;
            }

            var day = _locations[locationIndex].PartsOfDay.FirstOrDefault(p => p.Key == partOfDayKey);
            if (day == null) yield break;

            foreach (var level in day.Levels.OrderBy(l => l.Order))
            {
                yield return level.Address;
            }
        }

        public IEnumerable<string> GetPartOfDayKeys(int locationIndex)
        {
            if (locationIndex < 0 || locationIndex >= _locations.Count)
            {
                yield break;
            }

            foreach (var part in _locations[locationIndex].PartsOfDay)
            {
                yield return part.Key;
            }
        }

        /// <summary>
        /// Represents a level entry within a part-of-day node.
        /// </summary>
        public record LevelEntry(string Address, int Order);

        /// <summary>
        /// Represents a part-of-day node containing its levels.
        /// </summary>
        public record PartOfDayEntry(string Key, IReadOnlyList<LevelEntry> Levels);

        /// <summary>
        /// Represents a location node with its day partitions.
        /// </summary>
        public record LocationEntry(string Key, IReadOnlyList<PartOfDayEntry> PartsOfDay);

        public static HierarchicalLevelCatalog Empty => new(Enumerable.Empty<LocationEntry>());
    }
}
