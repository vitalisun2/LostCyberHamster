using System;
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
    }
}
