using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts;
using Assets.Scripts.Common.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

#nullable enable

namespace Assets.Scripts.System
{
    /// <summary>
    /// Builds the hierarchical level catalog from Addressables at runtime.
    /// </summary>
    public static class LevelCatalogRuntimeConfigurator
    {
        private static bool _reloadRequested;

        /// <summary>
        /// Explicitly requests catalog rebuild.
        /// </summary>
        public static void RequestReload()
        {
            _reloadRequested = true;
        }

        /// <summary>
        /// Ensures the hierarchical catalog is loaded and configured.
        /// </summary>
        public static async Task ApplyInspectorOverrideAsync(bool forceRebuild = false)
        {
            if (!forceRebuild && !_reloadRequested && LevelCatalogService.HasCatalog)
            {
                return;
            }

            var catalog = await BuildCatalogAsync();
            if (catalog == null)
            {
                Debug.LogWarning("[LevelCatalogRuntimeConfigurator] Failed to prepare hierarchical level catalog. The selection screens may not work as expected.");
                return;
            }

            LevelCatalogService.Configure(catalog);
            _reloadRequested = false;
        }

        private static async Task<HierarchicalLevelCatalog?> BuildCatalogAsync()
        {
            AsyncOperationHandle<IList<IResourceLocation>> handle = default;

            try
            {
                handle = Addressables.LoadResourceLocationsAsync(Consts.LevelsDaypart, typeof(TextAsset));
                var locations = await handle.Task;
                if (locations == null || locations.Count == 0)
                {
                    Debug.LogWarning($"[LevelCatalogRuntimeConfigurator] Label '{Consts.LevelsDaypart}' is empty.");
                    return null;
                }

                var layout = BuildLayout(locations);
                if (layout.Count == 0)
                {
                    Debug.LogWarning("[LevelCatalogRuntimeConfigurator] Unable to derive layout from Addressables entries.");
                    return null;
                }

                var locationDefinitions = layout
                    .OrderBy(location => location.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(location => new HierarchicalLevelCatalog.LocationDefinition(
                        location.Key,
                        location.Value
                            .OrderBy(part => ResolvePartOrder(part.Key))
                            .ThenBy(part => part.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(part => new HierarchicalLevelCatalog.PartDefinition(
                                part.Key,
                                part.Value
                                    .Select((address, index) => new HierarchicalLevelCatalog.LevelDefinition(address, index + 1))
                                    .ToList()))
                            .ToList()))
                    .ToList();

                if (locationDefinitions.Count == 0)
                {
                    return null;
                }

                return HierarchicalLevelCatalog.Factory.CreateCatalog(locationDefinitions);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelCatalogRuntimeConfigurator] Exception while preparing catalog: {ex.Message}");
                return null;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }


        private static int ResolvePartOrder(string partKey)
        {
            if (Enum.TryParse(typeof(PartOfDayEnum), partKey, true, out var value) && value is PartOfDayEnum enumValue)
            {
                return (int)enumValue;
            }

            return int.MaxValue;
        }

        private static Dictionary<string, SortedDictionary<string, SortedSet<string>>> BuildLayout(IList<IResourceLocation> locations)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var layout = new Dictionary<string, SortedDictionary<string, SortedSet<string>>>(comparer);

            foreach (var location in locations)
            {
                var address = location?.PrimaryKey ?? location?.InternalId;
                if (string.IsNullOrWhiteSpace(address))
                {
                    continue;
                }

                var normalized = address.Replace('\\', '/');
                var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 3)
                {
                    Debug.LogWarning($"[LevelCatalogRuntimeConfigurator] Address '{address}' does not match '<Location>/<Part>/<level_XX>' pattern.");
                    continue;
                }

                var partSegment = segments[^2];
                var locationSegment = segments[^3];

                if (!layout.TryGetValue(locationSegment, out var parts))
                {
                    parts = new SortedDictionary<string, SortedSet<string>>(comparer);
                    layout[locationSegment] = parts;
                }

                if (!parts.TryGetValue(partSegment, out var levels))
                {
                    levels = new SortedSet<string>(comparer);
                    parts[partSegment] = levels;
                }

                levels.Add(address);
            }

            return layout;
        }
    }
}









