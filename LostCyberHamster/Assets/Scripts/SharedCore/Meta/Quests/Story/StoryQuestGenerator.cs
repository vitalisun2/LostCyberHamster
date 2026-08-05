using System;
using System.Collections.Generic;
using GameManagement.Progress;

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
            if (string.IsNullOrWhiteSpace(questId))
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
        /// Создаёт случайный квест мастерства для завершённой, но не освоенной части суток.
        /// </summary>
        public bool TryCreateSecondaryDefinition(
            LevelProgressOverview progressOverview,
            out QuestDefinition definition)
        {
            if (progressOverview == null)
            {
                throw new ArgumentNullException(nameof(progressOverview));
            }

            // Собираем только доступные цели, которым ещё не присвоены три звезды на каждом уровне.
            var candidates = new List<PartProgress>();
            foreach (LocationProgress location in progressOverview.Locations)
            {
                foreach (PartProgress part in location.Parts)
                {
                    if (part.TotalLevels > 0 &&
                        part.IsCompleted &&
                        !part.IsMastered)
                    {
                        candidates.Add(part);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                definition = null;
                return false;
            }

            // Равновероятно выбираем одну часть из подготовленного списка.
            PartProgress selectedPart;
            lock (_random)
            {
                selectedPart = candidates[_random.Next(candidates.Count)];
            }

            definition = CreateMasteryDefinition(
                selectedPart.LocationId,
                selectedPart.PartOfDayId,
                selectedPart.TotalLevels);
            return true;
        }

        /// <summary>
        /// Восстанавливает случайный runtime-квест мастерства по сохранённому ID.
        /// </summary>
        public bool TryRestoreSecondaryDefinition(
            string questId,
            LevelProgressOverview progressOverview,
            out QuestDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                definition = null;
                return false;
            }

            if (progressOverview == null)
            {
                throw new ArgumentNullException(nameof(progressOverview));
            }

            // Сопоставляем ID со структурой прогресса без повторного случайного выбора.
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
