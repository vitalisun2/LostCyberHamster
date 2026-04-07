using UnityEngine;

namespace Assets.Scripts.System.LevelManagement
{
    /// <summary>
    /// Derives environment asset keys (background, background2, road, sky)
    /// from the current level's canonical LocationId and PartId in the catalog.
    /// Single source of truth for all environment texture key generation at runtime.
    /// </summary>
    public static class EnvironmentKeyResolver
    {
        /// <summary>
        /// Resolves background key for the current level using catalog descriptor.
        /// Falls back to first available location if the current level is not in the catalog.
        /// </summary>
        public static string BuildBackgroundKey()
        {
            var (locationSlug, daypartSlug) = ResolveCurrentSlugs();
            if (string.IsNullOrWhiteSpace(locationSlug) || string.IsNullOrWhiteSpace(daypartSlug))
                return null;

            return $"bg_{locationSlug}_{daypartSlug}";
        }

        /// <summary>
        /// Resolves background2 key for the current level using catalog descriptor.
        /// </summary>
        public static string BuildBackground2Key()
        {
            var (locationSlug, daypartSlug) = ResolveCurrentSlugs();
            if (string.IsNullOrWhiteSpace(locationSlug) || string.IsNullOrWhiteSpace(daypartSlug))
                return null;

            return $"bg_2_{locationSlug}_{daypartSlug}";
        }

        /// <summary>
        /// Resolves road key for the current level using catalog descriptor.
        /// </summary>
        public static string BuildRoadKey()
        {
            var (locationSlug, daypartSlug) = ResolveCurrentSlugs();
            if (string.IsNullOrWhiteSpace(locationSlug) || string.IsNullOrWhiteSpace(daypartSlug))
                return null;

            return $"rd_{locationSlug}_{daypartSlug}";
        }

        /// <summary>
        /// Resolves sky key for the current level using catalog descriptor.
        /// </summary>
        public static string BuildSkyKey()
        {
            var (locationSlug, daypartSlug) = ResolveCurrentSlugs();
            if (string.IsNullOrWhiteSpace(locationSlug) || string.IsNullOrWhiteSpace(daypartSlug))
                return null;

            return $"sky_{locationSlug}_{daypartSlug}";
        }

        /// <summary>
        /// Resolves canonical location slug and daypart slug from the current level descriptor.
        /// Uses catalog LocationId (e.g. "01_New_York") → ToLocationSlug → "new_york",
        /// and PartId (e.g. "Morning") → ToSlug → "morning".
        /// Falls back to the first available location in LocationInfoList if catalog lookup fails.
        /// </summary>
        private static (string LocationSlug, string DaypartSlug) ResolveCurrentSlugs()
        {
            // Primary path: use canonical catalog data
            var locationIndex = LevelManager.GetLocationIndex();
            var locationKey = LevelManager.GetLocationKey(locationIndex);
            var partOfDay = LevelManager.GetCurrentPartOfDay();

            if (!string.IsNullOrWhiteSpace(locationKey) && !string.IsNullOrWhiteSpace(partOfDay))
            {
                var locationSlug = LocationAssetFallback.ToLocationSlug(locationKey);
                var daypartSlug = LocationAssetFallback.ToSlug(partOfDay);

                if (!string.IsNullOrWhiteSpace(locationSlug) && !string.IsNullOrWhiteSpace(daypartSlug))
                    return (locationSlug, daypartSlug);
            }

            // Fallback: first location from LocationInfoList + catalog
            Debug.LogWarning("[EnvironmentKeyResolver] Catalog lookup failed, using fallback location.");
            var fallbackLocationKey = TryGetFallbackLocationKey();
            if (string.IsNullOrWhiteSpace(fallbackLocationKey))
                return (null, null);

            var fallbackSlug = LocationAssetFallback.ToLocationSlug(fallbackLocationKey);
            var fallbackDaypart = !string.IsNullOrWhiteSpace(partOfDay)
                ? LocationAssetFallback.ToSlug(partOfDay)
                : "morning";

            return (fallbackSlug, fallbackDaypart);
        }

        /// <summary>
        /// Tries to resolve the canonical location key for the fallback (first) location.
        /// Prefers catalog's GetLocationId(0) over display names.
        /// </summary>
        private static string TryGetFallbackLocationKey()
        {
            var key = LevelManager.GetLocationKey(0);
            if (!string.IsNullOrWhiteSpace(key))
                return key;

            // Last resort: hardcoded canonical key
            return "01_New_York";
        }
    }
}
