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
        /// Проверяет определения и уникальность идентификаторов каталога.
        /// </summary>
        public static void ValidateCatalog(
            IReadOnlyCollection<QuestDefinition> dailyDefinitions,
            IReadOnlyCollection<QuestDefinition> storyDefinitions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            ValidateCatalogGroup(
                dailyDefinitions,
                QuestCategory.Daily,
                ids);
            ValidateCatalogGroup(
                storyDefinitions,
                QuestCategory.Story,
                ids);
        }

        private static void ValidateCatalogGroup(
            IReadOnlyCollection<QuestDefinition> definitions,
            QuestCategory expectedCategory,
            ISet<string> ids)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (QuestDefinition definition in definitions)
            {
                ValidateDefinition(definition);
                if (definition.Category != expectedCategory)
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
    }
}
