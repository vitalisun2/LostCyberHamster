using System;
using System.Collections.Generic;
using System.Globalization;
using GameManagement;
using GameManagement.Progress;
using Vues.GameCore;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Создаёт runtime-описания активных сюжетных квестов.
    /// </summary>
    public sealed class StoryQuestGenerator
    {
        private const string PrimaryQuestIdPrefix = "story-primary-";
        private const string MasteryQuestIdPrefix = "story-mastery-";
        private const string NightPartOfDayId = "Night";

        private readonly Random _random = new();
        private readonly StoryQuestGenerationSettings _settings;

        /// <summary>
        /// Создаёт генератор с проверенными настройками сюжетных квестов.
        /// </summary>
        public StoryQuestGenerator(
            StoryQuestGenerationSettings settings)
        {
            QuestValidator.ValidateStoryGenerationSettings(settings);
            _settings = settings;
        }

        /// <summary>
        /// Создаёт последовательный квест для первой незавершённой части суток.
        /// </summary>
        public bool TryCreatePrimaryDefinition(
            LevelProgressOverview progressOverview,
            out QuestDefinition definition)
        {
            if (progressOverview == null)
            {
                throw new ArgumentNullException(nameof(progressOverview));
            }

            // Ищем первую незавершённую часть в порядке общей модели прогресса.
            foreach (LocationProgress location in progressOverview.Locations)
            {
                foreach (PartProgress part in location.Parts)
                {
                    if (part.TotalLevels == 0 || part.IsCompleted)
                    {
                        continue;
                    }

                    definition = CreatePrimaryDefinition(
                        location.LocationId,
                        part.PartOfDayId,
                        part.TotalLevels);
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>
        /// Восстанавливает последовательный runtime-квест по сохранённому ID.
        /// </summary>
        public bool TryRestorePrimaryDefinition(
            string questId,
            LevelProgressOverview progressOverview,
            out QuestDefinition definition)
        {
            // Проверяем принадлежность ID последовательному Story-слоту.
            if (string.IsNullOrWhiteSpace(questId) ||
                !questId.StartsWith(
                    PrimaryQuestIdPrefix,
                    StringComparison.Ordinal))
            {
                definition = null;
                return false;
            }

            if (progressOverview == null)
            {
                throw new ArgumentNullException(nameof(progressOverview));
            }

            // Сопоставляем ID со структурой общей модели прогресса.
            foreach (LocationProgress location in progressOverview.Locations)
            {
                foreach (PartProgress part in location.Parts)
                {
                    string candidateId = CreatePrimaryQuestId(
                        location.LocationId,
                        part.PartOfDayId);
                    if (!string.Equals(
                            candidateId,
                            questId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (part.TotalLevels == 0)
                    {
                        definition = null;
                        return false;
                    }

                    definition = CreatePrimaryDefinition(
                        location.LocationId,
                        part.PartOfDayId,
                        part.TotalLevels);
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>
        /// Создаёт случайный квест мастерства или доступного развития персонажа.
        /// </summary>
        public bool TryCreateSecondaryDefinition(
            LevelProgressOverview progressOverview,
            PlayerData playerData,
            IReadOnlyList<Skin> skins,
            IReadOnlyList<SuperAttackData> superAttacks,
            out QuestDefinition definition)
        {
            // Проверяем обязательные источники генерации.
            if (progressOverview == null)
            {
                throw new ArgumentNullException(nameof(progressOverview));
            }

            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            if (skins == null)
            {
                throw new ArgumentNullException(nameof(skins));
            }

            if (superAttacks == null)
            {
                throw new ArgumentNullException(nameof(superAttacks));
            }

            // Собираем все доступные цели мастерства и развития.
            var candidates = new List<QuestDefinition>();
            HashSet<string> claimedQuestIds = CollectClaimedQuestIds(
                playerData.QuestStates);
            AddMasteryCandidates(
                progressOverview,
                claimedQuestIds,
                candidates);
            AddDevelopmentCandidates(
                playerData,
                skins,
                superAttacks,
                claimedQuestIds,
                candidates);
            if (candidates.Count == 0)
            {
                definition = null;
                return false;
            }

            // Равновероятно выбираем одно готовое определение.
            lock (_random)
            {
                definition = candidates[_random.Next(candidates.Count)];
            }

            return true;
        }

        /// <summary>
        /// Восстанавливает вторичный runtime-квест по сохранённому ID.
        /// </summary>
        public bool TryRestoreSecondaryDefinition(
            string questId,
            LevelProgressOverview progressOverview,
            IReadOnlyList<Skin> skins,
            IReadOnlyList<SuperAttackData> superAttacks,
            out QuestDefinition definition)
        {
            // Проверяем сохранённый ID и обязательные каталоги.
            if (string.IsNullOrWhiteSpace(questId))
            {
                definition = null;
                return false;
            }

            if (progressOverview == null)
            {
                throw new ArgumentNullException(nameof(progressOverview));
            }

            if (skins == null)
            {
                throw new ArgumentNullException(nameof(skins));
            }

            if (superAttacks == null)
            {
                throw new ArgumentNullException(nameof(superAttacks));
            }

            // Сначала восстанавливаем цель мастерства из структуры уровней.
            if (TryRestoreMasteryDefinition(
                    questId,
                    progressOverview,
                    out definition))
            {
                return true;
            }

            // Затем восстанавливаем конкретную цель развития из её шаблона.
            return TryRestoreDevelopmentDefinition(
                questId,
                skins,
                superAttacks,
                out definition);
        }

        private void AddMasteryCandidates(
            LevelProgressOverview progressOverview,
            ISet<string> claimedQuestIds,
            ICollection<QuestDefinition> candidates)
        {
            foreach (LocationProgress location in progressOverview.Locations)
            {
                foreach (PartProgress part in location.Parts)
                {
                    if (part.TotalLevels == 0 ||
                        !part.IsCompleted ||
                        part.IsMastered)
                    {
                        continue;
                    }

                    string questId = CreateMasteryQuestId(
                        part.LocationId,
                        part.PartOfDayId);
                    if (claimedQuestIds.Contains(questId))
                    {
                        continue;
                    }

                    candidates.Add(CreateMasteryDefinition(
                        part.LocationId,
                        part.PartOfDayId,
                        part.TotalLevels));
                }
            }
        }

        private void AddDevelopmentCandidates(
            PlayerData playerData,
            IReadOnlyList<Skin> skins,
            IReadOnlyList<SuperAttackData> superAttacks,
            ISet<string> claimedQuestIds,
            ICollection<QuestDefinition> candidates)
        {
            foreach (StoryQuestDevelopmentTemplate template in
                     _settings.DevelopmentTemplates)
            {
                switch (template.StateId)
                {
                    case PlayerStateIds.PlayerLevel:
                        AddPlayerLevelCandidate(
                            template,
                            playerData,
                            claimedQuestIds,
                            candidates);
                        break;
                    case PlayerStateIds.SkinApplied:
                        AddSkinCandidates(
                            template,
                            playerData,
                            skins,
                            claimedQuestIds,
                            candidates);
                        break;
                    case PlayerStateIds.SuperAttackActive:
                        AddSuperAttackCandidates(
                            template,
                            playerData,
                            superAttacks,
                            claimedQuestIds,
                            candidates);
                        break;
                }
            }
        }

        private static void AddPlayerLevelCandidate(
            StoryQuestDevelopmentTemplate template,
            PlayerData playerData,
            ISet<string> claimedQuestIds,
            ICollection<QuestDefinition> candidates)
        {
            if (playerData.PlayerLevel == int.MaxValue)
            {
                return;
            }

            int targetLevel = playerData.PlayerLevel + 1;
            string questId = CreateDevelopmentQuestId(
                template.Id,
                targetLevel);
            if (claimedQuestIds.Contains(questId))
            {
                return;
            }

            candidates.Add(CreateDevelopmentDefinition(
                template,
                questId,
                PlayerStateEntityIds.Player,
                targetLevel,
                targetLevel.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddSkinCandidates(
            StoryQuestDevelopmentTemplate template,
            PlayerData playerData,
            IReadOnlyList<Skin> skins,
            ISet<string> claimedQuestIds,
            ICollection<QuestDefinition> candidates)
        {
            var purchasedSkinIds = new HashSet<int>(
                playerData.PurchasedSkinIds ?? new List<int>());
            foreach (Skin skin in skins)
            {
                if (skin == null ||
                    skin.Id <= 0 ||
                    !CharacterDevelopmentService.IsSkinUnlocked(skin.Id) ||
                    purchasedSkinIds.Contains(skin.Id))
                {
                    continue;
                }

                string questId = CreateDevelopmentQuestId(
                    template.Id,
                    skin.Id);
                if (claimedQuestIds.Contains(questId))
                {
                    continue;
                }

                candidates.Add(CreateDevelopmentDefinition(
                    template,
                    questId,
                    skin.Id.ToString(CultureInfo.InvariantCulture),
                    1,
                    skin.NameLocalizationKey));
            }
        }

        private static void AddSuperAttackCandidates(
            StoryQuestDevelopmentTemplate template,
            PlayerData playerData,
            IReadOnlyList<SuperAttackData> superAttacks,
            ISet<string> claimedQuestIds,
            ICollection<QuestDefinition> candidates)
        {
            foreach (SuperAttackData superAttack in superAttacks)
            {
                if (superAttack == null ||
                    superAttack.Id <= 0 ||
                    superAttack.Id == playerData.ActiveSuperAttackId ||
                    !SuperAttackService.IsUnlocked(superAttack.Id))
                {
                    continue;
                }

                string questId = CreateDevelopmentQuestId(
                    template.Id,
                    superAttack.Id);
                if (claimedQuestIds.Contains(questId))
                {
                    continue;
                }

                candidates.Add(CreateDevelopmentDefinition(
                    template,
                    questId,
                    superAttack.Id.ToString(CultureInfo.InvariantCulture),
                    1,
                    superAttack.NameLocalizationKey));
            }
        }

        private bool TryRestoreMasteryDefinition(
            string questId,
            LevelProgressOverview progressOverview,
            out QuestDefinition definition)
        {
            if (!questId.StartsWith(
                    MasteryQuestIdPrefix,
                    StringComparison.Ordinal))
            {
                definition = null;
                return false;
            }

            foreach (LocationProgress location in progressOverview.Locations)
            {
                foreach (PartProgress part in location.Parts)
                {
                    string candidateId = CreateMasteryQuestId(
                        part.LocationId,
                        part.PartOfDayId);
                    if (!string.Equals(
                            candidateId,
                            questId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (part.TotalLevels == 0)
                    {
                        definition = null;
                        return false;
                    }

                    definition = CreateMasteryDefinition(
                        part.LocationId,
                        part.PartOfDayId,
                        part.TotalLevels);
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private bool TryRestoreDevelopmentDefinition(
            string questId,
            IReadOnlyList<Skin> skins,
            IReadOnlyList<SuperAttackData> superAttacks,
            out QuestDefinition definition)
        {
            foreach (StoryQuestDevelopmentTemplate template in
                     _settings.DevelopmentTemplates)
            {
                if (!TryParseDevelopmentQuestId(
                        template.Id,
                        questId,
                        out int targetId))
                {
                    continue;
                }

                switch (template.StateId)
                {
                    case PlayerStateIds.PlayerLevel when targetId > 1:
                        definition = CreateDevelopmentDefinition(
                            template,
                            questId,
                            PlayerStateEntityIds.Player,
                            targetId,
                            targetId.ToString(CultureInfo.InvariantCulture));
                        return true;
                    case PlayerStateIds.SkinApplied:
                        if (TryFindSkin(skins, targetId, out Skin skin))
                        {
                            definition = CreateDevelopmentDefinition(
                                template,
                                questId,
                                skin.Id.ToString(CultureInfo.InvariantCulture),
                                1,
                                skin.NameLocalizationKey);
                            return true;
                        }

                        break;
                    case PlayerStateIds.SuperAttackActive:
                        if (TryFindSuperAttack(
                                superAttacks,
                                targetId,
                                out SuperAttackData superAttack))
                        {
                            definition = CreateDevelopmentDefinition(
                                template,
                                questId,
                                superAttack.Id.ToString(
                                    CultureInfo.InvariantCulture),
                                1,
                                superAttack.NameLocalizationKey);
                            return true;
                        }

                        break;
                }
            }

            definition = null;
            return false;
        }

        private static QuestDefinition CreateDevelopmentDefinition(
            StoryQuestDevelopmentTemplate template,
            string questId,
            string entityId,
            int requiredValue,
            string titleLocalizationArgumentKey = null)
        {
            var definition = new QuestDefinition
            {
                Id = questId,
                TitleLocalizationKey = template.TitleLocalizationKey,
                TitleLocalizationArguments =
                    string.IsNullOrWhiteSpace(titleLocalizationArgumentKey)
                        ? Array.Empty<string>()
                        : new[] { titleLocalizationArgumentKey },
                Category = QuestCategory.Story,
                Type = QuestType.PlayerState,
                StateId = template.StateId,
                EntityId = entityId,
                RequiredValue = requiredValue,
                TargetAmount = 1,
                RewardType = template.RewardType,
                RewardAmount = template.RewardAmount
            };
            QuestValidator.ValidateDefinition(definition);
            return definition;
        }

        private static HashSet<string> CollectClaimedQuestIds(
            IReadOnlyList<Quest> questStates)
        {
            var claimedQuestIds = new HashSet<string>(
                StringComparer.Ordinal);
            if (questStates == null)
            {
                return claimedQuestIds;
            }

            foreach (Quest quest in questStates)
            {
                if (quest != null &&
                    quest.IsRewardClaimed &&
                    !string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    claimedQuestIds.Add(quest.QuestId);
                }
            }

            return claimedQuestIds;
        }

        private static bool TryFindSkin(
            IReadOnlyList<Skin> skins,
            int skinId,
            out Skin skin)
        {
            foreach (Skin candidate in skins)
            {
                if (candidate != null && candidate.Id == skinId)
                {
                    skin = candidate;
                    return true;
                }
            }

            skin = null;
            return false;
        }

        private static bool TryFindSuperAttack(
            IReadOnlyList<SuperAttackData> superAttacks,
            int superAttackId,
            out SuperAttackData superAttack)
        {
            foreach (SuperAttackData candidate in superAttacks)
            {
                if (candidate != null && candidate.Id == superAttackId)
                {
                    superAttack = candidate;
                    return true;
                }
            }

            superAttack = null;
            return false;
        }

        private static string CreateDevelopmentQuestId(
            string templateId,
            int targetId)
        {
            return $"{templateId}-{targetId.ToString(CultureInfo.InvariantCulture)}";
        }

        private static bool TryParseDevelopmentQuestId(
            string templateId,
            string questId,
            out int targetId)
        {
            targetId = 0;
            string prefix = $"{templateId}-";
            return questId.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(
                       questId.Substring(prefix.Length),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out targetId) &&
                   targetId > 0;
        }

        private QuestDefinition CreatePrimaryDefinition(
            string locationId,
            string partOfDayId,
            int targetAmount)
        {
            bool isNight = string.Equals(
                partOfDayId,
                NightPartOfDayId,
                StringComparison.OrdinalIgnoreCase);
            var definition = new QuestDefinition
            {
                Id = CreatePrimaryQuestId(
                    locationId,
                    partOfDayId),
                TitleLocalizationKey = isNight
                    ? _settings.PrimaryNightTitleLocalizationKey
                    : _settings.PrimaryTitleLocalizationKey,
                TitleLocalizationArguments = isNight
                    ? Array.Empty<string>()
                    : new[] { locationId, partOfDayId },
                Category = QuestCategory.Story,
                Type = QuestType.LevelResult,
                RequiredLocationId = locationId,
                RequiredPartOfDayId = partOfDayId,
                CountUniqueLevels = true,
                RequiredStars = 1,
                TargetAmount = targetAmount,
                RewardType = _settings.PrimaryRewardType,
                RewardAmount = _settings.PrimaryRewardAmount
            };
            QuestValidator.ValidateDefinition(definition);
            return definition;
        }

        private QuestDefinition CreateMasteryDefinition(
            string locationId,
            string partOfDayId,
            int targetAmount)
        {
            var definition = new QuestDefinition
            {
                Id = CreateMasteryQuestId(
                    locationId,
                    partOfDayId),
                TitleLocalizationKey =
                    _settings.MasteryTitleLocalizationKey,
                TitleLocalizationArguments =
                    new[] { locationId, partOfDayId },
                Category = QuestCategory.Story,
                Type = QuestType.LevelResult,
                RequiredLocationId = locationId,
                RequiredPartOfDayId = partOfDayId,
                CountUniqueLevels = true,
                RequiredStars = 3,
                TargetAmount = targetAmount,
                RewardType = _settings.MasteryRewardType,
                RewardAmount = _settings.MasteryRewardAmount
            };
            QuestValidator.ValidateDefinition(definition);
            return definition;
        }

        private static string CreatePrimaryQuestId(
            string locationId,
            string partOfDayId)
        {
            return $"{PrimaryQuestIdPrefix}{locationId}-{partOfDayId}";
        }

        private static string CreateMasteryQuestId(
            string locationId,
            string partOfDayId)
        {
            return $"{MasteryQuestIdPrefix}{locationId}-{partOfDayId}";
        }
    }
}
