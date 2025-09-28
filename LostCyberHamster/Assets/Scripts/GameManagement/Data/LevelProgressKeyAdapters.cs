using System;
using Assets.Scripts.Common.Models;
using GameManagement.Progress;
using Vues.GameCore;

namespace GameManagement
{
    /// <summary>
    /// Helper methods that translate between legacy level identifiers ("level_XX")
    /// and the new typed progress keys.
    /// Consumers supply resolvers to keep the mapping logic testable and decoupled from specific catalogs.
    /// </summary>
    public static class LevelProgressKeyAdapters
    {
        public static bool TryFromLegacyLevelKey(
            string legacyLevelKey,
            int levelsPerLocation,
            Func<int, string?> locationIdResolver,
            Func<int, string?> partOfDayIdResolver,
            out LevelProgressKey key)
        {
            key = default;

            if (string.IsNullOrWhiteSpace(legacyLevelKey))
            {
                return false;
            }

            var normalized = legacyLevelKey.Trim();
            if (!normalized.StartsWith("level_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(normalized.Substring(6), out var numericId) || numericId <= 0)
            {
                return false;
            }

            if (levelsPerLocation <= 0)
            {
                return false;
            }

            var zeroBased = numericId - 1;
            var locationIndex = zeroBased / levelsPerLocation;
            var partOrder = zeroBased % levelsPerLocation;

            var locationId = locationIdResolver?.Invoke(locationIndex)?.Trim();
            var partId = partOfDayIdResolver?.Invoke(partOrder)?.Trim();

            if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(partId))
            {
                return false;
            }

            key = new LevelProgressKey(locationId, partId, 0);
            return true;
        }

        public static bool TryToLegacyLevelKey(
            LevelProgressKey key,
            int levelsPerLocation,
            Func<string, int> locationIndexResolver,
            Func<string, int> partOrderResolver,
            out string legacyLevelKey)
        {
            legacyLevelKey = string.Empty;

            if (levelsPerLocation <= 0 || key.LevelIndex != 0)
            {
                return false;
            }

            if (locationIndexResolver == null || partOrderResolver == null)
            {
                return false;
            }

            var locationIndex = locationIndexResolver(key.LocationId);
            var partOrder = partOrderResolver(key.PartOfDayId);

            if (locationIndex < 0 || partOrder < 0 || partOrder >= levelsPerLocation)
            {
                return false;
            }

            var legacyNumber = locationIndex * levelsPerLocation + partOrder + 1;
            legacyLevelKey = $"level_{legacyNumber:D2}";
            return true;
        }

        public static string? ResolvePartOfDayId(int partOrder)
        {
            var enumValue = partOrder + 1;
            if (!Enum.IsDefined(typeof(PartOfDayEnum), enumValue))
            {
                return null;
            }

            return ((PartOfDayEnum)enumValue).ToString();
        }

        public static int ResolveLegacyPartOrder(string partOfDayId)
        {
            if (string.IsNullOrWhiteSpace(partOfDayId))
            {
                return -1;
            }

            return Enum.TryParse(partOfDayId.Trim(), true, out PartOfDayEnum partOfDay)
                ? (int)partOfDay - 1
                : -1;
        }
    }
}
