using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using GameManagement.Progress;

namespace GameManagement
{
    public static class PlayerDataValidator
    {
        public static PlayerDataValidationResult Validate(PlayerData data)
        {
            if (data == null)
            {
                return PlayerDataValidationResult.Rejected("player_data_missing");
            }

            if (data.Money < 0 || data.Crystals < 0)
            {
                return PlayerDataValidationResult.Rejected("negative_resource_balance");
            }

            if (data.DailyTasks != null && data.DailyTasks.Any(quest => quest == null))
            {
                return PlayerDataValidationResult.Rejected("daily_quest_missing");
            }

            if (data.DailyTasks != null && data.DailyTasks.Any(quest => quest.IsRewardRecieved && !quest.IsCompleted))
            {
                return PlayerDataValidationResult.Rejected("daily_reward_without_completion");
            }

            if (data.PurchasedSkinIds != null && data.PurchasedSkinIds.Any(skinId => skinId < 0))
            {
                return PlayerDataValidationResult.Rejected("invalid_purchased_skin");
            }

            if (data.PurchasedSkinIds == null && data.AppliedSkinId != 0)
            {
                return PlayerDataValidationResult.Rejected("applied_skin_not_purchased");
            }

            if (data.PurchasedSkinIds != null &&
                !data.PurchasedSkinIds.Contains(data.AppliedSkinId) &&
                data.AppliedSkinId != 0)
            {
                return PlayerDataValidationResult.Rejected("applied_skin_not_purchased");
            }

            if (SkinManager.AvailableSkins.Count > 0 && data.PurchasedSkinIds != null)
            {
                var knownSkinIds = new HashSet<int>(SkinManager.AvailableSkins.Select(skin => skin.Id));
                if (data.PurchasedSkinIds.Any(skinId => !knownSkinIds.Contains(skinId)))
                {
                    return PlayerDataValidationResult.Rejected("unknown_purchased_skin");
                }
            }

            var storylineResult = ValidateStorylineProgress(data.StorylineQuestProgress);
            if (storylineResult != null)
            {
                return storylineResult;
            }

            var serializedProgressResult = ValidateSerializedProgress(data, out bool hasExactProgressDuplicates);
            if (serializedProgressResult?.Status == PlayerDataValidationStatus.Rejected)
            {
                return serializedProgressResult;
            }

            if (LevelCatalogService.HasCatalog &&
                !LevelCatalogService.TryFindLevel(data.CurrentLevel, out _))
            {
                return PlayerDataValidationResult.Rejected("unknown_current_level");
            }

            LevelProgressSnapshot progress;
            try
            {
                progress = data.HasSerializedProgressCollection && !hasExactProgressDuplicates
                    ? data.Progress
                    : LevelProgressSnapshot.Empty;
            }
            catch (Exception)
            {
                return PlayerDataValidationResult.Rejected("invalid_level_progress");
            }

            bool needsRepair = data.PurchasedSkinIds == null ||
                               data.DailyTasks == null ||
                               data.StorylineQuestProgress == null ||
                               !data.HasSerializedProgressCollection ||
                               !data.PurchasedSkinIds.Contains(0) ||
                               HasExactDuplicates(data.PurchasedSkinIds) ||
                               HasExactStorylineDuplicates(data.StorylineQuestProgress) ||
                               hasExactProgressDuplicates ||
                               NeedsCatalogProgress(progress);

            return needsRepair
                ? PlayerDataValidationResult.Repairable("safe_normalization_required")
                : PlayerDataValidationResult.Valid();
        }

        public static void RepairSafe(PlayerData data, PlayerDataValidationResult result)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Status == PlayerDataValidationStatus.Rejected)
            {
                throw new InvalidOperationException("Rejected player data cannot be repaired safely.");
            }

            if (result.Status == PlayerDataValidationStatus.Valid)
            {
                return;
            }

            data.PurchasedSkinIds ??= new List<int>();
            if (!data.PurchasedSkinIds.Contains(0))
            {
                data.PurchasedSkinIds.Add(0);
            }
            data.DailyTasks ??= new List<Vues.GameCore.Quest>();
            data.StorylineQuestProgress ??= new List<StorylineQuestProgressEntry>();
            data.EnsureSerializedProgressCollection();

            data.PurchasedSkinIds = data.PurchasedSkinIds.Distinct().ToList();
            data.StorylineQuestProgress = data.StorylineQuestProgress
                .GroupBy(entry => entry.QuestId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            RemoveExactProgressDuplicates(data);

            if (NeedsCatalogProgress(data.Progress))
            {
                data.Progress = LevelProgressSnapshot.CreateFromCatalog(LevelCatalogService.Catalog);
            }
        }

        private static PlayerDataValidationResult ValidateStorylineProgress(
            IReadOnlyCollection<StorylineQuestProgressEntry> progress)
        {
            if (progress == null)
            {
                return null;
            }

            if (progress.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.QuestId)))
            {
                return PlayerDataValidationResult.Rejected("invalid_storyline_progress");
            }

            bool hasConflict = progress
                .GroupBy(entry => entry.QuestId, StringComparer.Ordinal)
                .Any(group => group.Select(entry => entry.IsRewardClaimed).Distinct().Count() > 1);

            return hasConflict
                ? PlayerDataValidationResult.Rejected("conflicting_storyline_progress")
                : null;
        }

        private static PlayerDataValidationResult ValidateSerializedProgress(
            PlayerData data,
            out bool hasExactDuplicates)
        {
            hasExactDuplicates = false;
            if (!data.HasSerializedProgressCollection)
            {
                return null;
            }

            var entriesByKey = new Dictionary<LevelProgressKey, SerializableLevelProgressEntry>();
            foreach (var entry in data.SerializedProgress)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.LocationId) ||
                    string.IsNullOrWhiteSpace(entry.PartOfDayId) ||
                    entry.LevelIndex < 0 ||
                    entry.Stars < 0 ||
                    entry.Stars > LevelProgressEntry.MaxStars)
                {
                    return PlayerDataValidationResult.Rejected("invalid_level_progress");
                }

                var key = new LevelProgressKey(entry.LocationId, entry.PartOfDayId, entry.LevelIndex);
                if (!entriesByKey.TryGetValue(key, out var existing))
                {
                    entriesByKey.Add(key, entry);
                    continue;
                }

                if (existing.IsUnlocked != entry.IsUnlocked || existing.Stars != entry.Stars)
                {
                    return PlayerDataValidationResult.Rejected("conflicting_level_progress");
                }

                hasExactDuplicates = true;
            }

            return hasExactDuplicates
                ? PlayerDataValidationResult.Repairable("duplicate_level_progress")
                : null;
        }

        private static void RemoveExactProgressDuplicates(PlayerData data)
        {
            var uniqueEntries = new List<SerializableLevelProgressEntry>();
            var seenKeys = new HashSet<LevelProgressKey>();

            foreach (var entry in data.SerializedProgress)
            {
                var key = new LevelProgressKey(entry.LocationId, entry.PartOfDayId, entry.LevelIndex);
                if (seenKeys.Add(key))
                {
                    uniqueEntries.Add(entry);
                }
            }

            data.ReplaceSerializedProgress(uniqueEntries);
        }

        private static bool HasExactDuplicates(IEnumerable<int> values)
        {
            return values.GroupBy(value => value).Any(group => group.Count() > 1);
        }

        private static bool HasExactStorylineDuplicates(
            IEnumerable<StorylineQuestProgressEntry> progress)
        {
            return progress
                .GroupBy(entry => new { entry.QuestId, entry.IsRewardClaimed })
                .Any(group => group.Count() > 1);
        }

        private static bool NeedsCatalogProgress(LevelProgressSnapshot progress)
        {
            return LevelCatalogService.HasCatalog &&
                   !LevelCatalogService.Catalog.IsEmpty &&
                   progress.Entries.Count == 0;
        }
    }
}
