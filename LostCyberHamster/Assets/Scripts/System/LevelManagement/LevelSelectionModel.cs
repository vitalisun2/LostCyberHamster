using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Defines which catalog representation should be used to build the level selection snapshot.
    /// </summary>
    public enum LevelSelectionMode
    {
        Legacy,
        Hierarchical
    }

    /// <summary>
    /// Provides a read-only snapshot of locations, parts of day, and level identifiers
    /// for UI consumption. Supports both legacy flat layout and hierarchical layout.
    /// </summary>
    public sealed class LevelSelectionModel
    {
        private LevelSelectionModel(LevelSelectionMode mode, List<LocationView> locations)
        {
            Mode = mode;
            Locations = locations;
            FlattenedLevels = locations
                .SelectMany(location => location.Parts)
                .SelectMany(part => part.Levels)
                .ToList();
        }

        public LevelSelectionMode Mode { get; }

        public bool IsHierarchical => Mode == LevelSelectionMode.Hierarchical;

        public IReadOnlyList<LocationView> Locations { get; }

        public IReadOnlyList<LevelReference> FlattenedLevels { get; }

        public static LevelSelectionModel Create(LevelSelectionMode preferredMode = LevelSelectionMode.Hierarchical)
        {
            var mode = DetermineMode(preferredMode);
            var locations = mode == LevelSelectionMode.Hierarchical
                ? BuildHierarchical()
                : BuildLegacy();

            return new LevelSelectionModel(mode, locations);
        }

        public static LevelSelectionModel CreateLegacy() => Create(LevelSelectionMode.Legacy);

        public static LevelSelectionModel CreateHierarchical() => Create(LevelSelectionMode.Hierarchical);

        private static LevelSelectionMode DetermineMode(LevelSelectionMode preferredMode)
        {
            var hierarchicalAvailable = LevelCatalogService.Hierarchical is not null;
            if (preferredMode == LevelSelectionMode.Hierarchical && hierarchicalAvailable)
            {
                return LevelSelectionMode.Hierarchical;
            }

            return LevelSelectionMode.Legacy;
        }

        private static List<LocationView> BuildLegacy()
        {
            var locationInfos = LevelManager.LocationInfoList?.locations ?? Array.Empty<LocationInfo>();
            var locations = new List<LocationView>(locationInfos.Length);

            for (int locationIndex = 0; locationIndex < locationInfos.Length; locationIndex++)
            {
                var info = locationInfos[locationIndex];
                var parts = new List<PartView>();

                foreach (PartOfDayEnum part in Enum.GetValues(typeof(PartOfDayEnum)))
                {
                    var levelKeys = LevelManager.GetLevelsForPartOfDay(locationIndex, part.ToString())
                        .Select(key => new LevelReference(key, key))
                        .ToList();

                    parts.Add(new PartView(part.ToString(), part.ToString(), levelKeys));
                }

                locations.Add(new LocationView(
                    locationIndex,
                    info?.sysname ?? $"location_{locationIndex:D2}",
                    info?.name ?? info?.sysname ?? $"Location {locationIndex + 1}",
                    info?.image ?? string.Empty,
                    parts));
            }

            return locations;
        }

        private static List<LocationView> BuildHierarchical()
        {
            var catalog = LevelCatalogService.Hierarchical ?? HierarchicalLevelCatalog.Empty;
            var locationInfos = LevelManager.LocationInfoList?.locations ?? Array.Empty<LocationInfo>();
            var locationViews = new List<LocationView>();
            var locationCount = Math.Max(locationInfos.Length, catalog.Locations.Count);

            for (int index = 0; index < locationCount; index++)
            {
                var info = index < locationInfos.Length ? locationInfos[index] : null;
                var node = index < catalog.Locations.Count ? catalog.Locations[index] : null;

                if (node == null)
                {
                    // Fallback to legacy representation when hierarchical data is missing for the index.
                    var fallbackParts = new List<PartView>();
                    foreach (PartOfDayEnum part in Enum.GetValues(typeof(PartOfDayEnum)))
                    {
                        var levelKeys = LevelManager.GetLevelsForPartOfDay(index, part.ToString())
                            .Select(key => new LevelReference(key, key))
                            .ToList();

                        fallbackParts.Add(new PartView(part.ToString(), part.ToString(), levelKeys));
                    }

                    locationViews.Add(new LocationView(
                        index,
                        info?.sysname ?? $"location_{index:D2}",
                        info?.name ?? info?.sysname ?? $"Location {index + 1}",
                        info?.image ?? string.Empty,
                        fallbackParts));
                    continue;
                }

                var partViews = new List<PartView>();
                foreach (var part in node.PartsOfDay)
                {
                    var normalizedLevels = part.Levels
                        .OrderBy(level => level.Order)
                        .Select(level => new LevelReference(NormalizeLevelKey(level.Address), level.Address))
                        .Where(level => !string.IsNullOrEmpty(level.Key))
                        .ToList();

                    partViews.Add(new PartView(part.Key, ResolvePartDisplayName(part.Key), normalizedLevels));
                }

                locationViews.Add(new LocationView(
                    index,
                    node.Key ?? info?.sysname ?? $"location_{index:D2}",
                    info?.name ?? node.Key ?? $"Location {index + 1}",
                    info?.image ?? string.Empty,
                    partViews));
            }

            return locationViews;
        }

        private static string NormalizeLevelKey(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            var normalized = Path.GetFileNameWithoutExtension(address);
            return string.IsNullOrWhiteSpace(normalized) ? address : normalized;
        }

        private static string ResolvePartDisplayName(string partKey)
        {
            if (Enum.TryParse(typeof(PartOfDayEnum), partKey, true, out var value) && value is PartOfDayEnum part)
            {
                return part.ToString();
            }

            return partKey;
        }
    }

    public sealed class LocationView
    {
        public LocationView(int index, string key, string displayName, string imageAddress, IReadOnlyList<PartView> parts)
        {
            Index = index;
            Key = key;
            DisplayName = displayName;
            ImageAddress = imageAddress;
            Parts = parts;
        }

        public int Index { get; }

        public string Key { get; }

        public string DisplayName { get; }

        public string ImageAddress { get; }

        public IReadOnlyList<PartView> Parts { get; }
    }

    public sealed class PartView
    {
        public PartView(string key, string displayName, IReadOnlyList<LevelReference> levels)
        {
            Key = key;
            DisplayName = displayName;
            Levels = levels;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public IReadOnlyList<LevelReference> Levels { get; }
    }

    public sealed class LevelReference
    {
        public LevelReference(string key, string address)
        {
            Key = key;
            Address = address;
        }

        public string Key { get; }

        public string Address { get; }
    }
}
