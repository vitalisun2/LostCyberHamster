using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Загружает Daily-каталог, общую награду и настройки Story-генерации.
    /// </summary>
    public static class QuestCatalog
    {
        private static IReadOnlyList<QuestDefinition> _dailyDefinitions =
            Array.Empty<QuestDefinition>();
        private static DailyCommonRewardDefinition
            _dailyCommonRewardDefinition;
        private static StoryQuestGenerationSettings
            _storyGenerationSettings;
        private static Dictionary<string, QuestDefinition> _definitionsById =
            new();
        private static Task _loadTask;

        /// <summary>
        /// Дневные квесты из production JSON.
        /// </summary>
        public static IReadOnlyList<QuestDefinition> DailyDefinitions =>
            _dailyDefinitions;

        /// <summary>
        /// Контентная награда за завершение всего Daily-набора.
        /// </summary>
        public static DailyCommonRewardDefinition
            DailyCommonRewardDefinition => _dailyCommonRewardDefinition;

        /// <summary>
        /// Настройки генерируемых сюжетных квестов.
        /// </summary>
        public static StoryQuestGenerationSettings StoryGenerationSettings =>
            _storyGenerationSettings;

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

            List<QuestDefinition> dailyDefinitions =
                PrepareDailyDefinitions(data.DailyDefinitions);
            QuestValidator.ValidateCatalog(dailyDefinitions);
            QuestValidator.ValidateDailyCommonRewardDefinition(
                data.DailyCommonRewardDefinition);
            QuestValidator.ValidateStoryGenerationSettings(
                data.StoryGenerationSettings);

            // Публикуем проверенный Daily-каталог и настройки Story-генерации.
            _dailyDefinitions = dailyDefinitions.AsReadOnly();
            _dailyCommonRewardDefinition =
                data.DailyCommonRewardDefinition;
            _storyGenerationSettings = data.StoryGenerationSettings;
            var definitionsById =
                new Dictionary<string, QuestDefinition>();
            IndexDefinitions(dailyDefinitions, definitionsById);
            _definitionsById = definitionsById;
        }

        /// <summary>
        /// Ищет Daily-определение по стабильному идентификатору.
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

        private static List<QuestDefinition> PrepareDailyDefinitions(
            List<QuestDefinition> definitions)
        {
            definitions ??= new List<QuestDefinition>();

            foreach (QuestDefinition definition in definitions)
            {
                if (definition != null)
                {
                    definition.Category = QuestCategory.Daily;
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
