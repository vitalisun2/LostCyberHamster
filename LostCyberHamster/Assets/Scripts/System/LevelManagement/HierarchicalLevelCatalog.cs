using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Immutable catalog describing locations, parts of day and level addresses in the hierarchical model.
    /// </summary>
    public sealed class HierarchicalLevelCatalog
    {
        private readonly List<LocationEntry> _locations;
        private readonly Dictionary<string, LevelDescriptor> _levelsByAddress;
        private readonly Dictionary<string, LevelDescriptor> _levelsByKey;

        private HierarchicalLevelCatalog(IEnumerable<LocationEntry> locations)
        {
            if (locations == null)
            {
                throw new ArgumentNullException(nameof(locations));
            }

            _locations = locations.Select(NormalizeLocation).ToList();
            (_levelsByAddress, _levelsByKey) = BuildLevelLookups(_locations);
        }

        /// <summary>
        /// List of configured locations in display order.
        /// </summary>
        public IReadOnlyList<LocationEntry> Locations => _locations;

        public int LocationCount => _locations.Count;

        public bool IsEmpty => _locations.Count == 0;

        public bool TryGetLocation(int index, out LocationEntry location)
        {
            if (index >= 0 && index < _locations.Count)
            {
                location = _locations[index];
                return true;
            }

            location = default;
            return false;
        }

        public string GetLocationId(int index)
        {
            if (!TryGetLocation(index, out var location))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ResolveLocationId(location.Key, index);
        }

        public bool TryResolveLocationId(string locationId, out int locationIndex)
        {
            locationIndex = -1;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return false;
            }

            var normalized = locationId.Trim();
            for (int index = 0; index < _locations.Count; index++)
            {
                var entry = _locations[index];
                if (string.Equals(entry.Key, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ResolveLocationId(entry.Key, index), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    locationIndex = index;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPart(int locationIndex, string partKey, out int partIndex, out PartOfDayEntry part)
        {
            partIndex = -1;
            part = default;

            if (!TryGetLocation(locationIndex, out var location) || string.IsNullOrWhiteSpace(partKey))
            {
                return false;
            }

            var normalized = partKey.Trim();
            for (int index = 0; index < location.PartsOfDay.Count; index++)
            {
                var entry = location.PartsOfDay[index];
                var entryId = ResolvePartId(entry.Key, index);

                if (string.Equals(entry.Key, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entryId, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    partIndex = index;
                    part = entry;
                    return true;
                }
            }

            return false;
        }

        public string GetPartId(int locationIndex, int partIndex)
        {
            if (!TryGetLocation(locationIndex, out var location))
            {
                throw new ArgumentOutOfRangeException(nameof(locationIndex));
            }

            if (partIndex < 0 || partIndex >= location.PartsOfDay.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(partIndex));
            }

            return ResolvePartId(location.PartsOfDay[partIndex].Key, partIndex);
        }

        /// <summary>
        /// Пытается найти уровень по полному address или однозначному короткому ключу файла.
        /// </summary>
        public bool TryFindLevel(string identifier, out LevelDescriptor descriptor)
        {
            descriptor = default;
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            var normalizedAddress = NormalizeAddress(identifier);
            if (_levelsByAddress.TryGetValue(normalizedAddress, out descriptor))
            {
                return true;
            }

            var normalizedKey = NormalizeLevelKey(identifier);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return false;
            }

            return _levelsByKey.TryGetValue(normalizedKey, out descriptor);
        }

        public IEnumerable<LevelDescriptor> EnumerateLevels()
        {
            return _levelsByAddress.Values;
        }

        public IEnumerable<LevelDescriptor> EnumerateLevels(int locationIndex, int partIndex)
        {
            if (!TryGetLocation(locationIndex, out var location)
                || partIndex < 0 || partIndex >= location.PartsOfDay.Count)
            {
                return Enumerable.Empty<LevelDescriptor>();
            }

            var part = location.PartsOfDay[partIndex];
            return part.Levels
                .OrderBy(level => level.Order)
                .Select(level => _levelsByAddress[NormalizeAddress(level.Address)]);
        }

        public static HierarchicalLevelCatalog Empty { get; } = new HierarchicalLevelCatalog(new List<LocationEntry>());

        public static HierarchicalLevelCatalog Create(IEnumerable<LocationEntry> locations)
        {
            return new HierarchicalLevelCatalog(locations ?? throw new ArgumentNullException(nameof(locations)));
        }

        /// <summary>
        /// Factory helpers for building hierarchical catalog nodes from serialized definitions.
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

                var normalized = levels.Select((definition, index) => NormalizeLevel(definition, index))
                    .OrderBy(l => l.Order)
                    .ToList();
                return new PartOfDayEntry(key, normalized);
            }

            private static LocationEntry CreateLocationEntry(LocationDefinition definition)
            {
                var parts = definition.Parts.Select(CreatePartEntry).ToList();
                return new LocationEntry(definition.LocationKey, parts);
            }

            private static PartOfDayEntry CreatePartEntry(PartDefinition definition)
            {
                var levels = definition.Levels.Select((levelDef, index) => NormalizeLevel(levelDef, index)).ToList();
                return new PartOfDayEntry(definition.PartKey, levels.OrderBy(level => level.Order).ToList());
            }
        }

        public static HierarchicalLevelCatalog FromDictionary(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IEnumerable<string>>> layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var definitions = layout.Select(location => new LocationDefinition(
                location.Key,
                (location.Value ?? new Dictionary<string, IEnumerable<string>>())
                    .Select(part => new PartDefinition(part.Key,
                        (part.Value ?? Array.Empty<string>()).Select(address => new LevelDefinition(address)).ToList()))
                    .ToList()
            )).ToList();

            return Factory.CreateCatalog(definitions);
        }

        public record LevelEntry(string Address, int Order);

        public record PartOfDayEntry(string Key, IReadOnlyList<LevelEntry> Levels);

        public record LocationEntry(string Key, IReadOnlyList<PartOfDayEntry> PartsOfDay);

        public record LevelDefinition(string Address, int Order = 0);

        public record PartDefinition(string PartKey, IReadOnlyList<LevelDefinition> Levels);

        public record LocationDefinition(string LocationKey, IReadOnlyList<PartDefinition> Parts);

        public readonly struct LevelDescriptor
        {
            public LevelDescriptor(
                int locationIndex,
                string locationId,
                int partIndex,
                string partId,
                int levelIndex,
                string levelKey,
                string address)
            {
                LocationIndex = locationIndex;
                LocationId = locationId;
                PartIndex = partIndex;
                PartId = partId;
                LevelIndex = levelIndex;
                LevelKey = levelKey;
                Address = address;
            }

            public int LocationIndex { get; }
            public string LocationId { get; }
            public int PartIndex { get; }
            public string PartId { get; }
            public int LevelIndex { get; }
            public string LevelKey { get; }
            public string Address { get; }
            public int DisplayOrder => LevelIndex + 1;
        }

        private static (Dictionary<string, LevelDescriptor> ByAddress, Dictionary<string, LevelDescriptor> ByKey) BuildLevelLookups(IReadOnlyList<LocationEntry> locations)
        {
            var byAddress = new Dictionary<string, LevelDescriptor>(StringComparer.OrdinalIgnoreCase);
            var byKey = new Dictionary<string, LevelDescriptor>(StringComparer.OrdinalIgnoreCase);
            var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int locationIndex = 0; locationIndex < locations.Count; locationIndex++)
            {
                var location = locations[locationIndex];
                var locationId = ResolveLocationId(location.Key, locationIndex);

                for (int partIndex = 0; partIndex < location.PartsOfDay.Count; partIndex++)
                {
                    var part = location.PartsOfDay[partIndex];
                    var partId = ResolvePartId(part.Key, partIndex);
                    var orderedLevels = part.Levels.OrderBy(level => level.Order).ToList();

                    for (int levelIndex = 0; levelIndex < orderedLevels.Count; levelIndex++)
                    {
                        var level = orderedLevels[levelIndex];
                        var descriptor = new LevelDescriptor(
                            locationIndex,
                            locationId,
                            partIndex,
                            partId,
                            levelIndex,
                            NormalizeLevelKey(level.Address),
                            level.Address);

                        var addressKey = NormalizeAddress(level.Address);
                        byAddress[addressKey] = descriptor;

                        var levelKey = descriptor.LevelKey;
                        if (string.IsNullOrEmpty(levelKey) || ambiguousKeys.Contains(levelKey))
                        {
                            continue;
                        }

                        // Короткий ключ безопасен только пока он указывает ровно на один address.
                        if (byKey.ContainsKey(levelKey))
                        {
                            byKey.Remove(levelKey);
                            ambiguousKeys.Add(levelKey);
                            continue;
                        }

                        byKey[levelKey] = descriptor;
                    }
                }
            }

            return (byAddress, byKey);
        }

        private static LocationEntry NormalizeLocation(LocationEntry entry)
        {
            var parts = entry.PartsOfDay?
                .Select(part => new PartOfDayEntry(part.Key, part.Levels?
                    .Select(NormalizeLevel)
                    .OrderBy(level => level.Order)
                    .ToList() ?? new List<LevelEntry>()))
                .ToList() ?? new List<PartOfDayEntry>();

            return new LocationEntry(entry.Key, parts);
        }

        private static LevelEntry NormalizeLevel(LevelEntry level)
        {
            var order = level.Order <= 0 ? 1 : level.Order;
            return new LevelEntry(level.Address, order);
        }

        private static LevelEntry NormalizeLevel(LevelDefinition definition, int index)
        {
            var order = definition.Order > 0 ? definition.Order : index + 1;
            return new LevelEntry(definition.Address, order);
        }

        private static string ResolveLocationId(string locationKey, int locationIndex)
        {
            if (!string.IsNullOrWhiteSpace(locationKey))
            {
                return locationKey.Trim();
            }

            return $"location_{locationIndex:D2}";
        }

        private static string ResolvePartId(string partKey, int partIndex)
        {
            if (!string.IsNullOrWhiteSpace(partKey))
            {
                return partKey.Trim();
            }

            return ((PartOfDayEnum)(partIndex + 1)).ToString();
        }

        private static string NormalizeAddress(string address)
        {
            return string.IsNullOrWhiteSpace(address)
                ? string.Empty
                : address.Replace('\\', '/').Trim();
        }

        public static bool TryParseLevelAddress(string address, out string locationKey, out string partKey, out string levelKey)
        {
            locationKey = string.Empty;
            partKey = string.Empty;
            levelKey = string.Empty;

            var segments = GetAddressSegments(address);
            if (segments.Length != 3)
            {
                return false;
            }

            locationKey = segments[0].Trim();
            partKey = segments[1].Trim();
            levelKey = segments[2].Trim();

            return !string.IsNullOrWhiteSpace(locationKey)
                   && !string.IsNullOrWhiteSpace(partKey)
                   && !string.IsNullOrWhiteSpace(levelKey);
        }

        public static bool IsGameplayLevelAddress(string address)
        {
            return TryParseLevelAddress(address, out _, out _, out var levelKey)
                   && IsGameplayLevelKey(levelKey);
        }

        public static bool IsGameplayLevelKey(string levelKey)
        {
            const string prefix = "level_";

            if (string.IsNullOrWhiteSpace(levelKey))
            {
                return false;
            }

            var trimmed = levelKey.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || trimmed.Length != prefix.Length + 2)
            {
                return false;
            }

            for (int index = prefix.Length; index < trimmed.Length; index++)
            {
                if (!char.IsDigit(trimmed[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public static string NormalizeLevelKey(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return string.Empty;
            }

            var trimmed = identifier.Trim();
            var pathNormalized = trimmed.Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(pathNormalized);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName.Trim();
            }

            return pathNormalized;
        }

        private static string[] GetAddressSegments(string address)
        {
            var normalized = NormalizeAddress(address);
            return string.IsNullOrWhiteSpace(normalized)
                ? Array.Empty<string>()
                : normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
