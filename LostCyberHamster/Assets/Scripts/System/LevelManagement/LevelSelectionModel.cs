using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Provides a snapshot of the hierarchical catalog for UI consumption.
    /// </summary>
    public sealed class LevelSelectionModel
    {
        private LevelSelectionModel(List<LocationView> locations, List<LevelReference> flattenedLevels)
        {
            Locations = locations;
            FlattenedLevels = flattenedLevels;
        }

        public IReadOnlyList<LocationView> Locations { get; }

        public IReadOnlyList<LevelReference> FlattenedLevels { get; }

        public static LevelSelectionModel Create()
        {
            if (!LevelCatalogService.HasCatalog)
            {
                return new LevelSelectionModel(new List<LocationView>(), new List<LevelReference>());
            }

            var catalog = LevelCatalogService.Catalog;
            if (catalog.IsEmpty)
            {
                return new LevelSelectionModel(new List<LocationView>(), new List<LevelReference>());
            }

            var locationInfos = LevelManager.LocationInfoList?.locations ?? Array.Empty<LocationInfo>();
            var locationViews = new List<LocationView>(catalog.LocationCount);
            var flattenedLevels = new List<LevelReference>();

            for (int locationIndex = 0; locationIndex < catalog.LocationCount; locationIndex++)
            {
                if (!catalog.TryGetLocation(locationIndex, out var locationEntry))
                {
                    continue;
                }

                var info = locationIndex < locationInfos.Length ? locationInfos[locationIndex] : null;
                var locationId = catalog.GetLocationId(locationIndex);
                var locationKey = locationEntry.Key ?? string.Empty;
                var displayName = ResolveLocationDisplayName(info, locationEntry, locationIndex);
                var imageAddress = info?.image ?? string.Empty;

                var partViews = new List<PartView>(locationEntry.PartsOfDay?.Count ?? 0);

                for (int partIndex = 0; partIndex < (locationEntry.PartsOfDay?.Count ?? 0); partIndex++)
                {
                    var partEntry = locationEntry.PartsOfDay[partIndex];
                    if (partEntry == null)
                    {
                        continue;
                    }

                    var partId = catalog.GetPartId(locationIndex, partIndex);
                    var partKey = partEntry.Key ?? string.Empty;
                    var partDisplayName = ResolvePartDisplayName(partKey);

                    var levelReferences = new List<LevelReference>();
                    var orderedLevels = partEntry.Levels?
                        .OrderBy(level => level.Order)
                        .ToList() ?? new List<HierarchicalLevelCatalog.LevelEntry>();

                    for (int levelIndex = 0; levelIndex < orderedLevels.Count; levelIndex++)
                    {
                        var level = orderedLevels[levelIndex];
                        var normalizedKey = HierarchicalLevelCatalog.NormalizeLevelKey(level.Address);
                        if (string.IsNullOrEmpty(normalizedKey))
                        {
                            continue;
                        }

                        var reference = new LevelReference(
                            normalizedKey,
                            level.Address,
                            locationId,
                            locationIndex,
                            partId,
                            partIndex,
                            levelIndex,
                            level.Order > 0 ? level.Order : levelIndex + 1);

                        levelReferences.Add(reference);
                        flattenedLevels.Add(reference);
                    }

                    if (levelReferences.Count == 0)
                    {
                        continue;
                    }

                    partViews.Add(new PartView(
                        partIndex,
                        partId,
                        partKey,
                        partDisplayName,
                        levelReferences));
                }

                if (partViews.Count == 0)
                {
                    continue;
                }

                locationViews.Add(new LocationView(
                    locationIndex,
                    locationId,
                    locationKey,
                    displayName,
                    imageAddress,
                    partViews));
            }

            return new LevelSelectionModel(locationViews, flattenedLevels);
        }

        private static string ResolveLocationDisplayName(LocationInfo info, HierarchicalLevelCatalog.LocationEntry entry, int index)
        {
            if (!string.IsNullOrWhiteSpace(info?.name))
            {
                return info.name;
            }

            if (!string.IsNullOrWhiteSpace(entry?.Key))
            {
                return entry.Key;
            }

            return $"Location {index + 1}";
        }

        private static string ResolvePartDisplayName(string partKey)
        {
            if (Enum.TryParse(typeof(PartOfDayEnum), partKey, true, out var value) && value is PartOfDayEnum part)
            {
                return part.ToString();
            }

            return partKey;
        }

        public sealed class LocationView
        {
            public LocationView(
                int index,
                string id,
                string key,
                string displayName,
                string imageAddress,
                IReadOnlyList<PartView> parts)
            {
                Index = index;
                Id = id;
                Key = key;
                DisplayName = displayName;
                ImageAddress = imageAddress;
                Parts = parts ?? Array.Empty<PartView>();
            }

            public int Index { get; }

            public string Id { get; }

            public string Key { get; }

            public string DisplayName { get; }

            public string ImageAddress { get; }

            public IReadOnlyList<PartView> Parts { get; }
        }

        public sealed class PartView
        {
            public PartView(
                int index,
                string id,
                string key,
                string displayName,
                IReadOnlyList<LevelReference> levels)
            {
                Index = index;
                Id = id;
                Key = key;
                DisplayName = displayName;
                Levels = levels ?? Array.Empty<LevelReference>();
            }

            public int Index { get; }

            public string Id { get; }

            public string Key { get; }

            public string DisplayName { get; }

            public IReadOnlyList<LevelReference> Levels { get; }
        }

        public readonly struct LevelReference
        {
            public LevelReference(
                string key,
                string address,
                string locationId,
                int locationIndex,
                string partId,
                int partIndex,
                int levelIndex,
                int displayOrder)
            {
                Key = key;
                Address = address;
                LocationId = locationId;
                LocationIndex = locationIndex;
                PartId = partId;
                PartIndex = partIndex;
                LevelIndex = levelIndex;
                DisplayOrder = displayOrder;
            }

            public string Key { get; }

            public string Address { get; }

            public string LocationId { get; }

            public int LocationIndex { get; }

            public string PartId { get; }

            public int PartIndex { get; }

            public int LevelIndex { get; }

            public int DisplayOrder { get; }
        }
    }
}


