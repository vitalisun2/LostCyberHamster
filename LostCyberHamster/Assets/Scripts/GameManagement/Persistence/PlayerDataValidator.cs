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
        private const int RemovedSkateboardSkinId = 3;
        private const string RemovedSuperHitTutorialLevelAddress =
            "01_New_York/Morning/Tutorial Level 2";
        private const string FirstGameplayLevelAddress =
            "01_New_York/Morning/level_01";

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

            if (data.PurchasedSkinIds == null &&
                data.AppliedSkinId != 0 &&
                data.AppliedSkinId != RemovedSkateboardSkinId)
            {
                return PlayerDataValidationResult.Rejected("applied_skin_not_purchased");
            }

            if (data.PurchasedSkinIds != null &&
                !data.PurchasedSkinIds.Contains(data.AppliedSkinId) &&
                data.AppliedSkinId != 0 &&
                data.AppliedSkinId != RemovedSkateboardSkinId)
            {
                return PlayerDataValidationResult.Rejected("applied_skin_not_purchased");
            }

            if (SkinManager.AvailableSkins.Count > 0 && data.PurchasedSkinIds != null)
            {
                var knownSkinIds = new HashSet<int>(SkinManager.AvailableSkins.Select(skin => skin.Id));
                if (data.PurchasedSkinIds.Any(skinId =>
                        skinId != RemovedSkateboardSkinId &&
                        !knownSkinIds.Contains(skinId)))
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
                if (!SuperAttackService.TryGet(
                        data.ActiveSuperAttackId,
                        out _))
                {
                    return PlayerDataValidationResult.Rejected("unknown_active_super_attack");
                }
            }

            var developmentResult = ValidateDevelopmentState(data);
            if (developmentResult?.Status ==
                PlayerDataValidationStatus.Rejected)
            {
                return developmentResult;
            }

            var serializedProgressResult = ValidateSerializedProgress(data, out bool hasExactProgressDuplicates);
            if (serializedProgressResult?.Status == PlayerDataValidationStatus.Rejected)
            {
                return serializedProgressResult;
            }

            bool hasRemovedSuperHitTutorialLevel =
                IsRemovedSuperHitTutorialLevel(data.CurrentLevel);
            if (LevelCatalogService.HasCatalog &&
                !hasRemovedSuperHitTutorialLevel &&
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
                               developmentResult?.Status ==
                               PlayerDataValidationStatus.Repairable ||
                               data.PurchasedSkinIds == null ||
                               data.QuestStates == null ||
                               data.DailyQuestSet == null ||
                               data.DailyQuestSet?.ActiveQuestIds == null ||
                               data.DailyQuestSet?.LastGeneratedQuestIds == null ||
                               data.StoryQuestSet == null ||
                               data.StoryQuestSet?.GenerationDate == null ||
                               data.StoryQuestSet?.ActivePrimaryQuestId == null ||
                               data.StoryQuestSet?.ActiveSecondaryQuestId == null ||
                               !data.HasSerializedProgressCollection ||
                               data.AppliedSkinId == RemovedSkateboardSkinId ||
                               data.PurchasedSkinIds?.Contains(RemovedSkateboardSkinId) == true ||
                               !data.PurchasedSkinIds.Contains(0) ||
                               HasExactDuplicates(data.PurchasedSkinIds) ||
                               hasExactQuestStateDuplicates ||
                               hasExactProgressDuplicates ||
                               hasRemovedSuperHitTutorialLevel ||
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
            if (IsRemovedSuperHitTutorialLevel(data.CurrentLevel))
            {
                data.CurrentLevel = FirstGameplayLevelAddress;
                data.IsTutorialCompleted = true;
            }

            data.PurchasedSkinIds ??= new List<int>();
            data.PurchasedSkinIds.RemoveAll(skinId => skinId == RemovedSkateboardSkinId);
            if (data.AppliedSkinId == RemovedSkateboardSkinId)
            {
                data.AppliedSkinId = 0;
            }

            if (!data.PurchasedSkinIds.Contains(0))
            {
                data.PurchasedSkinIds.Add(0);
            }
            data.QuestStates ??= new List<Quest>();
            data.DailyQuestSet ??= new DailyQuestSetState();
            data.DailyQuestSet.ActiveQuestIds ??= new List<string>();
            data.DailyQuestSet.LastGeneratedQuestIds ??= new List<string>();
            data.StoryQuestSet ??= new StoryQuestSetState();
            data.StoryQuestSet.GenerationDate ??= string.Empty;
            data.StoryQuestSet.ActivePrimaryQuestId ??= string.Empty;
            data.StoryQuestSet.ActiveSecondaryQuestId ??= string.Empty;
            data.EnsureSerializedProgressCollection();

            data.PurchasedSkinIds = data.PurchasedSkinIds.Distinct().ToList();
            RepairDevelopmentState(data);
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

        private static bool IsRemovedSuperHitTutorialLevel(string levelAddress)
        {
            return string.Equals(
                levelAddress?.Replace('\\', '/').Trim(),
                RemovedSuperHitTutorialLevelAddress,
                StringComparison.OrdinalIgnoreCase);
        }

        private static PlayerDataValidationResult ValidateDevelopmentState(
            PlayerData data)
        {
            if (data.DevelopmentProgressVersion < 0 ||
                data.DevelopmentProgressVersion >
                CharacterDevelopmentService.CurrentProgressVersion)
            {
                return PlayerDataValidationResult.Rejected(
                    "unsupported_development_progress_version");
            }

            if (data.DevelopmentProgressVersion <
                CharacterDevelopmentService.CurrentProgressVersion)
            {
                return PlayerDataValidationResult.Repairable(
                    "development_progress_migration_required");
            }

            if (data.DevelopmentPoints < 0)
            {
                return PlayerDataValidationResult.Rejected(
                    "negative_development_points");
            }

            if (HasInvalidIds(data.UnlockedSkinIds) ||
                HasInvalidIds(data.UnlockedSuperAttackIds))
            {
                return PlayerDataValidationResult.Rejected(
                    "invalid_development_unlock");
            }

            if (HasUnknownSkinIds(data.UnlockedSkinIds) ||
                HasUnknownSuperAttackIds(data.UnlockedSuperAttackIds))
            {
                return PlayerDataValidationResult.Rejected(
                    "unknown_development_unlock");
            }

            bool needsRepair = data.UnlockedSkinIds == null ||
                               data.UnlockedSuperAttackIds == null ||
                               !data.UnlockedSkinIds.Contains(
                                   CharacterDevelopmentService.DefaultSkinId) ||
                               HasExactDuplicates(data.UnlockedSkinIds) ||
                               HasExactDuplicates(data.UnlockedSuperAttackIds) ||
                               data.PurchasedSkinIds?.Any(
                                   skinId =>
                                       !data.UnlockedSkinIds.Contains(skinId)) == true ||
                               data.ActiveSuperAttackId > 0 &&
                               !data.UnlockedSuperAttackIds.Contains(
                                   data.ActiveSuperAttackId);

            return needsRepair
                ? PlayerDataValidationResult.Repairable(
                    "development_progress_normalization_required")
                : null;
        }

        private static void RepairDevelopmentState(PlayerData data)
        {
            bool isLegacy = data.DevelopmentProgressVersion <
                            CharacterDevelopmentService.CurrentProgressVersion;

            data.UnlockedSkinIds ??= new List<int>();
            data.UnlockedSuperAttackIds ??= new List<int>();

            // Ownership и active selection сохраняют доступ при migration/repair.
            data.UnlockedSkinIds.AddRange(data.PurchasedSkinIds);
            if (!data.UnlockedSkinIds.Contains(
                    CharacterDevelopmentService.DefaultSkinId))
            {
                data.UnlockedSkinIds.Add(
                    CharacterDevelopmentService.DefaultSkinId);
            }

            if (data.ActiveSuperAttackId > 0 &&
                !data.UnlockedSuperAttackIds.Contains(
                    data.ActiveSuperAttackId))
            {
                data.UnlockedSuperAttackIds.Add(
                    data.ActiveSuperAttackId);
            }

            if (isLegacy)
            {
                int earnedPoints = Math.Max(0, data.PlayerLevel - 1);
                int spentOnActiveAbility = data.ActiveSuperAttackId > 0
                    ? 1
                    : 0;
                data.DevelopmentPoints = Math.Max(
                    0,
                    earnedPoints - spentOnActiveAbility);
            }

            data.UnlockedSkinIds = data.UnlockedSkinIds
                .Distinct()
                .ToList();
            data.UnlockedSuperAttackIds = data.UnlockedSuperAttackIds
                .Distinct()
                .ToList();
            data.DevelopmentProgressVersion =
                CharacterDevelopmentService.CurrentProgressVersion;
        }

        private static bool HasInvalidIds(IReadOnlyCollection<int> values)
        {
            return values?.Any(value => value < 0) == true;
        }

        private static bool HasUnknownSkinIds(
            IReadOnlyCollection<int> values)
        {
            if (values == null || SkinManager.AvailableSkins.Count == 0)
            {
                return false;
            }

            var knownIds = new HashSet<int>(
                SkinManager.AvailableSkins.Select(skin => skin.Id));
            return values.Any(value => !knownIds.Contains(value));
        }

        private static bool HasUnknownSuperAttackIds(
            IReadOnlyCollection<int> values)
        {
            return values?.Any(
                value => !SuperAttackService.TryGet(value, out _)) == true &&
                   SuperAttackService.Items.Count > 0;
        }

        private static bool NeedsCatalogProgress(LevelProgressSnapshot progress)
        {
            return LevelCatalogService.HasCatalog &&
                   !LevelCatalogService.Catalog.IsEmpty &&
                   progress.Entries.Count == 0;
        }
    }
}
