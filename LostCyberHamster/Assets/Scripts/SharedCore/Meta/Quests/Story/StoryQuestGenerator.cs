using System;
using GameManagement.Progress;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Создаёт runtime-описания активных сюжетных квестов.
    /// </summary>
    public sealed class StoryQuestGenerator
    {
        private const string PrimaryQuestIdPrefix = "story-primary-";
        private const string NightPartOfDayId = "Night";

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

        private static string CreatePrimaryQuestId(
            string locationId,
            string partOfDayId)
        {
            return $"{PrimaryQuestIdPrefix}{locationId}-{partOfDayId}";
        }
    }
}
