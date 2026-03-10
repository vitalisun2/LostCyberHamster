using System;
using System.Linq;
using System.Text;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Utility helpers for resolving fallback asset keys and labels when location-specific data is missing.
    /// </summary>
    public static class LocationAssetFallback
    {
        public static string TryBuildFallbackLabel(string originalLabel, string currentLocation, string fallbackLocation)
        {
            if (string.IsNullOrWhiteSpace(originalLabel) ||
                string.IsNullOrWhiteSpace(currentLocation) ||
                string.IsNullOrWhiteSpace(fallbackLocation))
            {
                return null;
            }

            var trimmedOriginal = originalLabel.Trim();
            var trimmedCurrent = currentLocation.Trim();
            var trimmedFallback = fallbackLocation.Trim();

            if (!trimmedOriginal.StartsWith(trimmedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var suffix = trimmedOriginal.Substring(trimmedCurrent.Length);
            return string.Concat(trimmedFallback, suffix);
        }

        public static string ToSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var previousWasUnderscore = false;

            foreach (var ch in value.Trim())
            {
                if (char.IsWhiteSpace(ch) || ch == '-' || ch == '.')
                {
                    if (!previousWasUnderscore)
                    {
                        builder.Append('_');
                        previousWasUnderscore = true;
                    }

                    continue;
                }

                builder.Append(char.ToLowerInvariant(ch));
                previousWasUnderscore = false;
            }

            return builder.ToString().Trim('_');
        }

        public static string TryBuildFallbackBackgroundKey(string originalKey, string fallbackLocationName, string partOfDay)
        {
            var fallbackSlug = ToSlug(fallbackLocationName);
            if (string.IsNullOrWhiteSpace(fallbackSlug))
            {
                return null;
            }

            var suffix = ExtractBackgroundSuffix(originalKey);
            if (string.IsNullOrWhiteSpace(suffix))
            {
                suffix = ToSlug(partOfDay);
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }

            return $"bg_{fallbackSlug}_{suffix}";
        }

        public static string BuildRoadKey(string fallbackLocationName, string partOfDay)
        {
            var fallbackSlug = ToSlug(fallbackLocationName);
            if (string.IsNullOrWhiteSpace(fallbackSlug))
            {
                return null;
            }

            var suffix = ToSlug(partOfDay);
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }

            return $"rd_{fallbackSlug}_{suffix}";
        }

        public static string BuildSkyKey(string fallbackLocationName, string partOfDay)
        {
            var fallbackSlug = ToSlug(fallbackLocationName);
            if (string.IsNullOrWhiteSpace(fallbackSlug))
            {
                return null;
            }

            var suffix = ToSlug(partOfDay);
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }

            return $"sky_{fallbackSlug}_{suffix}";
        }

        private static string ExtractBackgroundSuffix(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var segments = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return null;
            }

            return segments[^1];
        }

        /// <summary>
        /// Converts a location directory name like "01_New_York" into a slug "new_york",
        /// stripping the leading numeric prefix if present.
        /// </summary>
        public static string ToLocationSlug(string locationDirName)
        {
            if (string.IsNullOrWhiteSpace(locationDirName))
                return string.Empty;

            var parts = locationDirName.Trim().Split('_', StringSplitOptions.RemoveEmptyEntries);
            int startIndex = 0;
            if (parts.Length > 0 && int.TryParse(parts[0], out _))
                startIndex = 1;

            if (startIndex >= parts.Length)
                return ToSlug(locationDirName);

            return string.Join("_", parts.Skip(startIndex).Select(p => p.ToLowerInvariant()));
        }

        /// <summary>
        /// Builds background sprite key: bg_{locationSlug}_{daypart}.
        /// </summary>
        public static string BuildBackgroundKey(string locationDirName, string partOfDay)
        {
            var slug = ToLocationSlug(locationDirName);
            var daySuffix = ToSlug(partOfDay);
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(daySuffix))
                return null;

            return $"bg_{slug}_{daySuffix}";
        }
    }
}
