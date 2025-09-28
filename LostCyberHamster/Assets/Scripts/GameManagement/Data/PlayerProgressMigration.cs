using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using UnityEngine;

namespace GameManagement.Progress
{
    public static class PlayerProgressMigration
    {
        public static void Initialize(PlayerData playerData)
        {
            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            try
            {
                if (LevelCatalogService.HasCatalog)
                {
                    var snapshot = LevelProgressSnapshot.CreateFromCatalog(LevelCatalogService.Catalog);
                    snapshot = ApplyLegacyStars(snapshot, playerData.LevelStars);
                    playerData.Progress = snapshot;
                }
                else
                {
                    _ = playerData.Progress; // triggers fallback restoration from legacy data
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerProgressMigration] Failed to initialise snapshot: {ex.Message}");
                _ = playerData.Progress;
            }
        }

        private static LevelProgressSnapshot ApplyLegacyStars(LevelProgressSnapshot baseSnapshot, List<int> legacyStars)
        {
            if (legacyStars == null || legacyStars.Count == 0)
            {
                return baseSnapshot;
            }

            var orderedEntries = baseSnapshot.Entries
                .OrderBy(entry => entry.Key.LocationId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Key.PartOfDayId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Key.LevelIndex)
                .ToList();

            var updated = new List<LevelProgressEntry>(orderedEntries.Count);

            for (int i = 0; i < orderedEntries.Count; i++)
            {
                var entry = orderedEntries[i];
                var stars = i < legacyStars.Count ? Mathf.Clamp(legacyStars[i], 0, LevelProgressEntry.MaxStars) : entry.Stars;
                var isUnlocked = entry.IsUnlocked || i < legacyStars.Count;
                updated.Add(new LevelProgressEntry(entry.Key, isUnlocked, stars));
            }

            return new LevelProgressSnapshot(updated);
        }
    }
}
