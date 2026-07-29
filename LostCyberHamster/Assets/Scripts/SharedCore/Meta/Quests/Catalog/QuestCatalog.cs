using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Загружает и предоставляет определения реальных квестов.
    /// </summary>
    public static class QuestCatalog
    {
        private static IReadOnlyList<QuestDefinition> _dailyDefinitions =
            Array.Empty<QuestDefinition>();
        private static IReadOnlyList<QuestDefinition> _storyDefinitions =
            Array.Empty<QuestDefinition>();
        private static Dictionary<string, QuestDefinition> _definitionsById =
            new();
        private static Task _loadTask;

        /// <summary>
        /// Дневные квесты из production JSON.
        /// </summary>
        public static IReadOnlyList<QuestDefinition> DailyDefinitions =>
            _dailyDefinitions;

        /// <summary>
        /// Сюжетные квесты из production JSON.
        /// </summary>
        public static IReadOnlyList<QuestDefinition> StoryDefinitions =>
            _storyDefinitions;

        /// <summary>
        /// Загружает единственный production-каталог квестов.
        /// </summary>
        public static Task LoadAsync()
        {
            return _loadTask ??= LoadInternalAsync();
        }

        private static async Task LoadInternalAsync()
        {
            TextAsset asset =
                await Addressables.LoadAssetAsync<TextAsset>("questData").Task;
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Файл каталога квестов не загружен.");
            }

            QuestCatalogData data =
                JsonUtility.FromJson<QuestCatalogData>(asset.text);
            if (data == null)
            {
                throw new InvalidOperationException(
                    "Каталог квестов не загружен.");
            }

            var definitionsById =
                new Dictionary<string, QuestDefinition>();
            _dailyDefinitions = PrepareDefinitions(
                data.DailyDefinitions,
                QuestCategory.Daily,
                definitionsById);
            _storyDefinitions = PrepareDefinitions(
                data.StoryDefinitions,
                QuestCategory.Story,
                definitionsById);
            _definitionsById = definitionsById;
        }

        /// <summary>
        /// Возвращает определения выбранной категории.
        /// </summary>
        public static IReadOnlyList<QuestDefinition> GetDefinitions(
            QuestCategory category)
        {
            return category switch
            {
                QuestCategory.Daily => DailyDefinitions,
                QuestCategory.Story => StoryDefinitions,
                _ => Array.Empty<QuestDefinition>()
            };
        }

        /// <summary>
        /// Ищет определение по стабильному идентификатору.
        /// </summary>
        public static bool TryGet(
            string questId,
            out QuestDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                definition = null;
                return false;
            }

            return _definitionsById.TryGetValue(
                questId,
                out definition);
        }

        private static IReadOnlyList<QuestDefinition> PrepareDefinitions(
            List<QuestDefinition> definitions,
            QuestCategory category,
            Dictionary<string, QuestDefinition> definitionsById)
        {
            if (definitions == null)
            {
                return Array.Empty<QuestDefinition>();
            }

            foreach (QuestDefinition definition in definitions)
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.Id))
                {
                    throw new InvalidOperationException(
                        "Каталог содержит квест без Id.");
                }

                definition.Category = category;
                QuestSystem.ValidateDefinition(definition);
                if (!definitionsById.TryAdd(
                        definition.Id,
                        definition))
                {
                    throw new InvalidOperationException(
                        $"Id квеста повторяется: {definition.Id}.");
                }
            }

            return definitions.AsReadOnly();
        }
    }
}
