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
            _locations = locations?.Select(NormalizeLocation).ToList() ?? throw new ArgumentNullException(nameof(locations));
        }

        /// <summary>
        /// Hierarchical catalog supports variable level counts; legacy constant is not applicable.
        /// </summary>
        public int LevelsPerLocation => 0;

        /// <summary>
        /// Exposes the internal locations for read-only scenarios.
        /// </summary>
        public IReadOnlyList<LocationEntry> Locations => _locations;

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

            if (string.IsNullOrWhiteSpace(partOfDayKey))
            {
                yield break;
            }

            var day = _locations[locationIndex].PartsOfDay.FirstOrDefault(p => string.Equals(p.Key, partOfDayKey, StringComparison.OrdinalIgnoreCase));
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
        /// Returns the logical key (e.g. "01_New_York") for the specified location index if known.
        /// </summary>
        public string? GetLocationKey(int locationIndex)
        {
            if (locationIndex < 0 || locationIndex >= _locations.Count)
            {
                return null;
            }

            return _locations[locationIndex].Key;
        }

        /// <summary>
        /// Returns the part-of-day entry matching the provided key or null if it is not defined.
        /// </summary>
        public PartOfDayEntry? GetPartOfDay(int locationIndex, string partOfDayKey)
        {
            if (locationIndex < 0 || locationIndex >= _locations.Count || string.IsNullOrWhiteSpace(partOfDayKey))
            {
                return null;
            }

            return _locations[locationIndex].PartsOfDay.FirstOrDefault(p => string.Equals(p.Key, partOfDayKey, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Factory helpers for building hierarchical catalog nodes.
        /// </summary>
        public static class Factory
        {
            public static HierarchicalLevelCatalog CreateCatalog(IEnumerable<LocationDefinition> definitions)
            {
                if (definitions == null)
                {
                    throw new ArgumentNullException(nameof(definitions));
                }

                var entries = definitions.Select(CreateLocationEntry).ToList();
                return new HierarchicalLevelCatalog(entries);
            }

            public static LocationEntry CreateLocation(string key, IEnumerable<PartDefinition> parts)
            {
                if (parts == null)
                {
                    throw new ArgumentNullException(nameof(parts));
                }

                return new LocationEntry(key, parts.Select(CreatePartEntry).ToList());
            }

            public static PartOfDayEntry CreatePart(string key, IEnumerable<string> levelAddresses)
            {
                if (levelAddresses == null)
                {
                    throw new ArgumentNullException(nameof(levelAddresses));
                }

                var levels = levelAddresses.Select((address, index) => new LevelEntry(address, index + 1)).ToList();
                return new PartOfDayEntry(key, levels);
            }

            public static PartOfDayEntry CreatePart(string key, IEnumerable<LevelDefinition> levels)
            {
                if (levels == null)
                {
                    throw new ArgumentNullException(nameof(levels));
                }

                var normalized = levels.Select((definition, index) => NormalizeLevel(definition, index)).OrderBy(l => l.Order).ToList();
                return new PartOfDayEntry(key, normalized);
            }

            private static LocationEntry CreateLocationEntry(LocationDefinition definition)
            {
                var parts = definition.Parts?.Select(CreatePartEntry).ToList() ?? new List<PartOfDayEntry>();
                return new LocationEntry(definition.LocationKey, parts);
            }

            private static PartOfDayEntry CreatePartEntry(PartDefinition definition)
            {
                var levels = definition.Levels?.Select((levelDef, index) => NormalizeLevel(levelDef, index)).OrderBy(l => l.Order).ToList() ?? new List<LevelEntry>();
                return new PartOfDayEntry(definition.PartKey, levels);
            }
        }

        public record LevelEntry(string Address, int Order);

        public record PartOfDayEntry(string Key, IReadOnlyList<LevelEntry> Levels);

        public record LocationEntry(string Key, IReadOnlyList<PartOfDayEntry> PartsOfDay);

        public record LevelDefinition(string Address, int Order = 0);

        public record PartDefinition(string PartKey, IReadOnlyList<LevelDefinition>? Levels);

        public record LocationDefinition(string LocationKey, IReadOnlyList<PartDefinition>? Parts);

        public static HierarchicalLevelCatalog Empty { get; } = new HierarchicalLevelCatalog(new List<LocationEntry>());

        public static HierarchicalLevelCatalog FromDictionary(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IEnumerable<string>>> layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var definitions = layout.Select(location => new LocationDefinition(
                location.Key,
                location.Value?.Select(part => new PartDefinition(part.Key, part.Value?.Select(address => new LevelDefinition(address)).ToList())).ToList()
            )).ToList();

            return Factory.CreateCatalog(definitions);
        }

        private static LocationEntry NormalizeLocation(LocationEntry entry)
        {
            var parts = entry.PartsOfDay?.Select(part => new PartOfDayEntry(part.Key, part.Levels?.Select(NormalizeLevel).OrderBy(l => l.Order).ToList() ?? new List<LevelEntry>())).ToList() ?? new List<PartOfDayEntry>();
            return new LocationEntry(entry.Key, parts);
        }

        private static LevelEntry NormalizeLevel(LevelEntry level)
        {
            var order = level.Order;
            if (order <= 0)
            {
                order = 1;
            }

            return new LevelEntry(level.Address, order);
        }

        private static LevelEntry NormalizeLevel(LevelDefinition definition, int index)
        {
            var order = definition.Order > 0 ? definition.Order : index + 1;
            return new LevelEntry(definition.Address, order);
        }
    }
}
