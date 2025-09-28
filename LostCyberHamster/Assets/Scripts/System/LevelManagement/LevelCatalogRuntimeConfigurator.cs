using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts;
using Assets.Scripts.System.FeatureFlags;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Builds and applies the hierarchical level catalog based on inspector overrides.
    /// </summary>
    public static class LevelCatalogRuntimeConfigurator
    {
        private static bool? _inspectorOverride;

        /// <summary>
        /// Registers the desired catalog mode coming from the inspector toggle.
        /// </summary>
        public static void SetInspectorOverride(bool useHierarchical)
        {
            _inspectorOverride = useHierarchical;
        }

        /// <summary>
        /// Applies the previously registered override by switching the catalog and feature flag.
        /// </summary>
        public static async Task ApplyInspectorOverrideAsync(bool persist = false)
        {
            if (!_inspectorOverride.HasValue)
            {
                return;
            }

            if (_inspectorOverride.Value)
            {
                var catalog = await EnsureHierarchicalCatalogAsync();
                if (catalog == null)
                {
                    Debug.LogWarning("[LevelCatalogRuntimeConfigurator] Failed to configure hierarchical catalog. Falling back to legacy mode.");
                    LevelCatalogService.UseLegacyCatalog();
                    DayPartLevelsFeature.SetEnabled(false, persist: false);
                    return;
                }

                LevelCatalogService.ConfigureHierarchicalCatalog(catalog, activate: true);
                DayPartLevelsFeature.SetEnabled(true, persist);
            }
            else
            {
                LevelCatalogService.UseLegacyCatalog();
                DayPartLevelsFeature.SetEnabled(false, persist);
            }
        }

        private static async Task<HierarchicalLevelCatalog?> EnsureHierarchicalCatalogAsync()
        {
            if (LevelCatalogService.Hierarchical is { } existing)
            {
                return existing;
            }

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
                            .OrderBy(part => part.Key, StringComparer.OrdinalIgnoreCase)
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

                // Store the original address so Addressables can resolve it later.
                levels.Add(address);
            }

            return layout;
        }
    }
}









