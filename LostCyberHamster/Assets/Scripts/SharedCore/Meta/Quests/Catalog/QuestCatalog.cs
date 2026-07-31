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
            if (_loadTask == null ||
                _loadTask.IsCanceled ||
                _loadTask.IsFaulted)
            {
                _loadTask = LoadInternalAsync();
            }

            return _loadTask;
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

            List<QuestDefinition> dailyDefinitions = PrepareDefinitions(
                data.DailyDefinitions,
                QuestCategory.Daily);
            List<QuestDefinition> storyDefinitions = PrepareDefinitions(
                data.StoryDefinitions,
                QuestCategory.Story);
            QuestValidator.ValidateCatalog(
                dailyDefinitions,
                storyDefinitions);

            // Публикуем проверенные списки и индекс по Id.
            _dailyDefinitions = dailyDefinitions.AsReadOnly();
            _storyDefinitions = storyDefinitions.AsReadOnly();
            var definitionsById =
                new Dictionary<string, QuestDefinition>();
            IndexDefinitions(dailyDefinitions, definitionsById);
            IndexDefinitions(storyDefinitions, definitionsById);
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

        private static List<QuestDefinition> PrepareDefinitions(
            List<QuestDefinition> definitions,
            QuestCategory category)
        {
            definitions ??= new List<QuestDefinition>();

            foreach (QuestDefinition definition in definitions)
            {
                if (definition != null)
                {
                    definition.Category = category;
                }
            }

            return definitions;
        }

        private static void IndexDefinitions(
            IEnumerable<QuestDefinition> definitions,
            IDictionary<string, QuestDefinition> definitionsById)
        {
            foreach (QuestDefinition definition in definitions)
            {
                definitionsById.Add(definition.Id, definition);
            }
        }
    }
}
