using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using GameManagement.Progress;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Формирует UI-модель каталога с готовыми состояниями прогресса.
    /// </summary>
    public sealed class LevelSelectionModel
    {
        private LevelSelectionModel(
            List<LocationView> locations,
            IReadOnlyList<LevelProgress> flattenedLevels)
        {
            Locations = locations;
            FlattenedLevels = flattenedLevels;
        }

        public IReadOnlyList<LocationView> Locations { get; }

        public IReadOnlyList<LevelProgress> FlattenedLevels { get; }

        /// <summary>
        /// Создаёт готовую для UI модель каталога вместе с текущим прогрессом.
        /// </summary>
        public static LevelSelectionModel Create()
        {
            if (!LevelCatalogService.HasCatalog)
            {
                return new LevelSelectionModel(
                    new List<LocationView>(),
                    Array.Empty<LevelProgress>());
            }

            var catalog = LevelCatalogService.Catalog;
            var locationInfos = LevelManager.LocationInfoList?.locations ?? Array.Empty<LocationInfo>();
            var progressOverview = LevelManager.ProgressOverview;
            var locationViews = new List<LocationView>(
                progressOverview.Locations.Count);

            // Добавляет данные отображения к готовой доменной структуре локаций.
            foreach (var locationProgress in progressOverview.Locations)
            {
                var locationIndex = locationProgress.LocationIndex;
                if (!catalog.TryGetLocation(locationIndex, out var locationEntry))
                {
                    continue;
                }

                var info = locationIndex < locationInfos.Length ? locationInfos[locationIndex] : null;
                var locationKey = locationEntry.Key ?? string.Empty;
                var displayName = ResolveLocationDisplayName(info, locationEntry, locationIndex);
                var imageAddress = info?.image ?? string.Empty;

                var partViews = new List<PartView>(
                    locationProgress.Parts.Count);

                // Строит UI-ссылки по уже рассчитанным состояниям частей и уровней.
                foreach (var partProgress in locationProgress.Parts)
                {
                    var partIndex = partProgress.PartIndex;
                    if (partIndex < 0 ||
                        partIndex >= locationEntry.PartsOfDay.Count)
                    {
                        continue;
                    }

                    var partEntry = locationEntry.PartsOfDay[partIndex];
                    var partKey = partEntry.Key ?? string.Empty;
                    var partDisplayName = ResolvePartDisplayName(partKey);

                    var levels = partProgress.Levels;
                    if (levels.Count == 0)
                    {
                        continue;
                    }

                    partViews.Add(new PartView(
                        partKey,
                        partDisplayName,
                        partProgress));
                }

                if (partViews.Count == 0)
                {
                    continue;
                }

                locationViews.Add(new LocationView(
                    locationKey,
                    displayName,
                    imageAddress,
                    partViews,
                    locationProgress));
            }

            return new LevelSelectionModel(
                locationViews,
                progressOverview.Levels);
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

    }
}
