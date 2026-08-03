using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using GameManagement.Progress;
using Vues.GameCore;
using Vues.GameCore.Quests;

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

            var questStatesResult = ValidateQuestStates(
                data.QuestStates,
                out bool hasExactQuestStateDuplicates);
            if (questStatesResult?.Status ==
                PlayerDataValidationStatus.Rejected)
            {
                return questStatesResult;
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

            if (data.ActiveSuperAttackId < 0)
            {
                return PlayerDataValidationResult.Rejected("invalid_active_super_attack");
            }

            if (data.ActiveSuperAttackId > 0 && SuperAttackService.Items.Count > 0)
            {
                if (!SuperAttackService.TryGet(data.ActiveSuperAttackId, out SuperAttackData superAttack))
                {
                    return PlayerDataValidationResult.Rejected("unknown_active_super_attack");
                }

                if (data.PlayerLevel >= 1 &&
                    data.PlayerLevel < superAttack.RequiredPlayerLevel)
                {
                    return PlayerDataValidationResult.Rejected("locked_active_super_attack");
                }
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

            bool needsRepair = data.ExperiencePoints < 0 ||
                               data.PlayerLevel < 1 ||
                               data.PurchasedSkinIds == null ||
                               data.QuestStates == null ||
                               data.DailyQuestSet == null ||
                               data.DailyQuestSet?.ActiveQuestIds == null ||
                               data.DailyQuestSet?.LastGeneratedQuestIds == null ||
                               !data.HasSerializedProgressCollection ||
                               !data.PurchasedSkinIds.Contains(0) ||
                               HasExactDuplicates(data.PurchasedSkinIds) ||
                               hasExactQuestStateDuplicates ||
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

            data.ExperiencePoints = Math.Max(0, data.ExperiencePoints);
            data.PlayerLevel = Math.Max(1, data.PlayerLevel);
            data.PurchasedSkinIds ??= new List<int>();
            if (!data.PurchasedSkinIds.Contains(0))
            {
                data.PurchasedSkinIds.Add(0);
            }
            data.QuestStates ??= new List<Quest>();
            data.DailyQuestSet ??= new DailyQuestSetState();
            data.DailyQuestSet.ActiveQuestIds ??= new List<string>();
            data.DailyQuestSet.LastGeneratedQuestIds ??= new List<string>();
            data.EnsureSerializedProgressCollection();

            data.PurchasedSkinIds = data.PurchasedSkinIds.Distinct().ToList();
            data.QuestStates = data.QuestStates
                .GroupBy(entry => entry.QuestId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            RemoveExactProgressDuplicates(data);

            if (NeedsCatalogProgress(data.Progress))
            {
                data.Progress = LevelProgressSnapshot.CreateFromCatalog(LevelCatalogService.Catalog);
            }
        }

        private static PlayerDataValidationResult ValidateQuestStates(
            IReadOnlyCollection<Quest> states,
            out bool hasExactDuplicates)
        {
            hasExactDuplicates = false;
            if (states == null)
            {
                return null;
            }

            var statesById =
                new Dictionary<string, Quest>(
                    StringComparer.Ordinal);
            foreach (Quest state in states)
            {
                if (!QuestValidator.IsSavedQuestValid(state))
                {
                    return PlayerDataValidationResult.Rejected(
                        "invalid_quest_state");
                }

                if (!statesById.TryGetValue(
                        state.QuestId,
                        out Quest existing))
                {
                    statesById.Add(state.QuestId, state);
                    continue;
                }

                if (existing.CurrentProgress != state.CurrentProgress ||
                    existing.IsCompleted != state.IsCompleted ||
                    existing.IsRewardClaimed != state.IsRewardClaimed)
                {
                    return PlayerDataValidationResult.Rejected(
                        "conflicting_quest_state");
                }

                hasExactDuplicates = true;
            }

            return hasExactDuplicates
                ? PlayerDataValidationResult.Repairable(
                    "duplicate_quest_state")
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

        private static bool NeedsCatalogProgress(LevelProgressSnapshot progress)
        {
            return LevelCatalogService.HasCatalog &&
                   !LevelCatalogService.Catalog.IsEmpty &&
                   progress.Entries.Count == 0;
        }
    }
}
