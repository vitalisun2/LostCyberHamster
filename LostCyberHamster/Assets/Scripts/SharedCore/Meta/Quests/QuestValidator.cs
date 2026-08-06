using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Централизует проверку данных и связей системы квестов.
    /// </summary>
    public static class QuestValidator
    {
        /// <summary>
        /// Проверяет обязательные данные определения квеста.
        /// </summary>
        public static void ValidateDefinition(
            QuestDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id) ||
                string.IsNullOrWhiteSpace(
                    definition.TitleLocalizationKey))
            {
                throw new ArgumentException(
                    "Описание квеста содержит пустые обязательные данные.",
                    nameof(definition));
            }

            if (!Enum.IsDefined(
                    typeof(QuestType),
                    definition.Type) ||
                definition.Type == QuestType.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.Type,
                    "Тип квеста не задан.");
            }

            if (definition.Category == QuestCategory.Daily &&
                string.IsNullOrWhiteSpace(
                    definition.DailyMechanicId))
            {
                throw new ArgumentException(
                    "Механика дневного квеста не задана.",
                    nameof(definition));
            }

            if (definition.Category == QuestCategory.Daily &&
                (!Enum.IsDefined(
                     typeof(DailyQuestDifficulty),
                     definition.DailyDifficulty) ||
                 definition.DailyDifficulty ==
                 DailyQuestDifficulty.None))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.DailyDifficulty,
                    "Сложность дневного квеста не задана.");
            }

            if (definition.Type == QuestType.ActionCounter &&
                !GameplayActionIds.IsKnown(definition.ActionId))
            {
                throw new ArgumentException(
                    "Действие квеста-счётчика не поддерживается.",
                    nameof(definition));
            }

            if (definition.Type == QuestType.LevelResult &&
                definition.RequiredLevelId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RequiredLevelId,
                    "Номер уровня не должен быть отрицательным.");
            }

            if (definition.Type == QuestType.LevelResult &&
                (definition.RequiredStars < 1 ||
                 definition.RequiredStars > 3))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RequiredStars,
                    "Количество звёзд должно быть от 1 до 3.");
            }

            if (definition.Type == QuestType.LevelResult &&
                definition.CountUniqueLevels &&
                (string.IsNullOrWhiteSpace(
                     definition.RequiredLocationId) ||
                 string.IsNullOrWhiteSpace(
                     definition.RequiredPartOfDayId)))
            {
                throw new ArgumentException(
                    "Квест на разные уровни должен задавать локацию и часть суток.",
                    nameof(definition));
            }

            if (definition.Type == QuestType.PlayerState &&
                (!PlayerStateIds.IsKnown(definition.StateId) ||
                 string.IsNullOrWhiteSpace(definition.EntityId)))
            {
                throw new ArgumentException(
                    "Состояние игрока или идентификатор сущности не поддерживается.",
                    nameof(definition));
            }

            if (definition.Type == QuestType.PlayerState &&
                definition.RequiredValue <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RequiredValue,
                    "Требуемое значение состояния должно быть положительным.");
            }

            if (definition.Type == QuestType.PlayerState &&
                definition.TargetAmount != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.TargetAmount,
                    "Цель квеста состояния игрока должна быть равна одному.");
            }

            if (definition.TargetAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.TargetAmount,
                    "Цель квеста должна быть положительной.");
            }

            if (definition.RewardType != ResourceType.Coins &&
                definition.RewardType != ResourceType.Crystals)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RewardType,
                    "Тип награды квеста не поддерживается.");
            }

            if (definition.RewardAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RewardAmount,
                    "Награда квеста должна быть положительной.");
            }
        }

        /// <summary>
        /// Проверяет настройки генерируемых сюжетных квестов.
        /// </summary>
        public static void ValidateStoryGenerationSettings(
            StoryQuestGenerationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(
                    settings.PrimaryTitleLocalizationKey) ||
                string.IsNullOrWhiteSpace(
                    settings.PrimaryNightTitleLocalizationKey) ||
                string.IsNullOrWhiteSpace(
                    settings.MasteryTitleLocalizationKey))
            {
                throw new ArgumentException(
                    "Настройки Story-генерации содержат пустой ключ локализации.",
                    nameof(settings));
            }

            bool hasUnsupportedReward =
                settings.PrimaryRewardType != ResourceType.Coins &&
                settings.PrimaryRewardType != ResourceType.Crystals ||
                settings.MasteryRewardType != ResourceType.Coins &&
                settings.MasteryRewardType != ResourceType.Crystals;
            if (hasUnsupportedReward)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "Тип награды Story-генерации не поддерживается.");
            }

            if (settings.PrimaryRewardAmount <= 0 ||
                settings.MasteryRewardAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "Награда Story-генерации должна быть положительной.");
            }

            ValidateStoryDevelopmentTemplates(
                settings.DevelopmentTemplates);
        }

        private static void ValidateStoryDevelopmentTemplates(
            IReadOnlyCollection<StoryQuestDevelopmentTemplate> templates)
        {
            const int requiredTemplateCount = 3;

            // Проверяем полноту универсального набора шаблонов.
            if (templates == null)
            {
                throw new ArgumentNullException(nameof(templates));
            }

            if (templates.Count != requiredTemplateCount)
            {
                throw new ArgumentException(
                    "Story-генерация должна содержать три шаблона развития.",
                    nameof(templates));
            }

            // Готовим обязательный набор состояний и уникальные ID.
            var requiredStateIds = new HashSet<string>(
                StringComparer.Ordinal)
            {
                PlayerStateIds.PlayerLevel,
                PlayerStateIds.SkinApplied,
                PlayerStateIds.SuperAttackActive
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (StoryQuestDevelopmentTemplate template in templates)
            {
                ValidateStoryDevelopmentTemplate(template);
                if (!ids.Add(template.Id))
                {
                    throw new ArgumentException(
                        $"ID Story-шаблона развития повторяется: {template.Id}.",
                        nameof(templates));
                }

                if (!requiredStateIds.Remove(template.StateId))
                {
                    throw new ArgumentException(
                        $"Состояние Story-шаблона развития не поддерживается " +
                        $"или повторяется: {template.StateId}.",
                        nameof(templates));
                }
            }
        }

        private static void ValidateStoryDevelopmentTemplate(
            StoryQuestDevelopmentTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            // Проверяем обязательные данные шаблона.
            if (string.IsNullOrWhiteSpace(template.Id) ||
                string.IsNullOrWhiteSpace(template.TitleLocalizationKey) ||
                string.IsNullOrWhiteSpace(template.StateId))
            {
                throw new ArgumentException(
                    "Story-шаблон развития содержит пустые обязательные данные.",
                    nameof(template));
            }

            // Проверяем награду будущего runtime-квеста.
            if (template.RewardType != ResourceType.Coins &&
                template.RewardType != ResourceType.Crystals)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(template),
                    template.RewardType,
                    "Тип награды Story-шаблона не поддерживается.");
            }

            if (template.RewardAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(template),
                    template.RewardAmount,
                    "Награда Story-шаблона должна быть положительной.");
            }
        }

        /// <summary>
        /// Проверяет соответствие определения и стратегии.
        /// </summary>
        public static void ValidateBinding(
            QuestDefinition definition,
            IQuestStrategy strategy)
        {
            ValidateDefinition(definition);
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (strategy.Type != definition.Type)
            {
                throw new ArgumentException(
                    $"Стратегия {strategy.Type} не подходит " +
                    $"типу {definition.Type}.",
                    nameof(strategy));
            }
        }

        /// <summary>
        /// Проверяет сохранённое состояние одного квеста.
        /// </summary>
        public static bool IsSavedQuestValid(Quest quest)
        {
            return quest != null &&
                   !string.IsNullOrWhiteSpace(quest.QuestId) &&
                   quest.CurrentProgress >= 0 &&
                   (!quest.IsRewardClaimed || quest.IsCompleted);
        }

        /// <summary>
        /// Проверяет Daily-определения и уникальность их идентификаторов.
        /// </summary>
        public static void ValidateCatalog(
            IReadOnlyCollection<QuestDefinition> dailyDefinitions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (dailyDefinitions != null)
            {
                foreach (QuestDefinition definition in dailyDefinitions)
                {
                    ValidateDefinition(definition);
                    if (definition.Category != QuestCategory.Daily)
                    {
                        throw new InvalidOperationException(
                            $"Квест {definition.Id} находится " +
                            "в неверной категории.");
                    }

                    if (!ids.Add(definition.Id))
                    {
                        throw new InvalidOperationException(
                            $"Id квеста повторяется: {definition.Id}.");
                    }
                }
            }

            ValidateDailyDifficultyCoverage(dailyDefinitions);
        }

        /// <summary>
        /// Проверяет контентную награду за завершение Daily-набора.
        /// </summary>
        public static void ValidateDailyCommonRewardDefinition(
            DailyCommonRewardDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.RewardType != ResourceType.Coins &&
                definition.RewardType != ResourceType.Crystals)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RewardType,
                    "Тип общей Daily-награды не поддерживается.");
            }

            if (definition.RewardAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RewardAmount,
                    "Общая Daily-награда должна быть положительной.");
            }
        }

        private static void ValidateDailyDifficultyCoverage(
            IReadOnlyCollection<QuestDefinition> definitions)
        {
            // Собираем сложности, представленные в дневном каталоге.
            var availableDifficulties =
                new HashSet<DailyQuestDifficulty>();
            if (definitions != null)
            {
                foreach (QuestDefinition definition in definitions)
                {
                    if (definition != null)
                    {
                        availableDifficulties.Add(
                            definition.DailyDifficulty);
                    }
                }
            }

            // Проверяем наличие кандидата для каждого дневного слота.
            foreach (DailyQuestDifficulty difficulty in new[]
                     {
                         DailyQuestDifficulty.Simple,
                         DailyQuestDifficulty.Medium,
                         DailyQuestDifficulty.Hard
                     })
            {
                if (!availableDifficulties.Contains(difficulty))
                {
                    throw new InvalidOperationException(
                        $"Каталог дневных квестов не содержит " +
                        $"квест сложности {difficulty}.");
                }
            }
        }
    }
}
