using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using Vues.GameCore.Quests;

namespace Vues.GameCore
{
    /// <summary>
    /// Связывает активные квесты с событиями игры, сейвом и UI.
    /// </summary>
    public static class QuestManager
    {
        private static readonly IReadOnlyDictionary<QuestType, IQuestStrategy>
            _strategies = new Dictionary<QuestType, IQuestStrategy>
            {
                [QuestType.ActionCounter] =
                    new ActionCounterQuestStrategy(),
                [QuestType.LevelResult] =
                    new LevelResultQuestStrategy(),
                [QuestType.PlayerState] =
                    new PlayerStateQuestStrategy()
            };

        private static readonly QuestAttemptBuffer _attemptBuffer = new();
        private static readonly DailyQuestService _dailyQuestService = new(
            new DailyQuestGenerator(),
            new DailyQuestScheduler());
        private static readonly PlayerExperienceService
            _playerExperienceService = new();

        private static IReadOnlyList<Quest> _activeQuests =
            Array.Empty<Quest>();
        private static IReadOnlyList<Quest> _dailyQuests =
            Array.Empty<Quest>();
        private static IReadOnlyList<Quest> _storyQuests =
            Array.Empty<Quest>();

        public static IReadOnlyList<Quest> DailyQuests =>
            _dailyQuests;

        public static IReadOnlyList<Quest> StoryQuests =>
            _storyQuests;

        /// <summary>
        /// Загружает каталог и восстанавливает активные квесты.
        /// </summary>
        public static async Task Init()
        {
            await QuestCatalog.LoadAsync();
            if (QuestCatalog.DailyDefinitions.Count == 0 ||
                QuestCatalog.StoryDefinitions.Count == 0)
            {
                throw new InvalidOperationException(
                    "MVP-каталог должен содержать дневные квесты " +
                    "и сюжетные квесты.");
            }

            bool dailySetChanged = InitDailyQuestSet(DateTime.Now);
            BindActiveQuests();
            if (dailySetChanged)
            {
                PlayerProgressCommitter.Commit(
                    CheckpointReason.DailyQuestSetRotated);
            }
        }

        /// <summary>
        /// Проверяет смену локального дня и обновляет активные дневные квесты.
        /// </summary>
        public static void Update()
        {
            if (!_dailyQuestService.IsInitialized)
            {
                return;
            }

            // Обновляем сохранённый набор после наступления нового дня.
            List<string> previousIds =
                _dailyQuestService.State.ActiveQuestIds.ToList();
            if (!_dailyQuestService.Update(
                    DateTime.Now,
                    GameDataManager.PlayerData.QuestStates))
            {
                return;
            }

            CompleteDailySetChange(previousIds);
        }

        private static void CompleteDailySetChange(
            IReadOnlyCollection<string> previousIds)
        {
            // Переподключаем активные квесты и сохраняем новый набор.
            ApplyDailySetChange(previousIds, hadGeneratedSet: true);
            BindActiveQuests(discardAttempt: false);
            PlayerProgressCommitter.Commit(
                CheckpointReason.DailyQuestSetRotated);

            // Уведомляем открытые экраны об изменении набора.
            GameEventsManager.DailyQuestSetChanged();
        }

        /// <summary>
        /// Подключает обработчики игровых событий.
        /// </summary>
        public static void OnEnable()
        {
            GameEventsManager.OnLevelStarted += HandleLevelStarted;
            GameEventsManager.OnActionCounterQuestEvent +=
                HandleActionCounterQuestEvent;
            GameEventsManager.OnLevelCompleted += HandleLevelCompleted;
            GameEventsManager.OnSkinPurchased += HandleSkinPurchased;
            GameDataManager.PlayerDataReplaced += HandlePlayerDataReplaced;
        }

        /// <summary>
        /// Отключает обработчики и очищает текущую попытку.
        /// </summary>
        public static void OnDisable()
        {
            GameEventsManager.OnLevelStarted -= HandleLevelStarted;
            GameEventsManager.OnActionCounterQuestEvent -=
                HandleActionCounterQuestEvent;
            GameEventsManager.OnLevelCompleted -= HandleLevelCompleted;
            GameEventsManager.OnSkinPurchased -= HandleSkinPurchased;
            GameDataManager.PlayerDataReplaced -= HandlePlayerDataReplaced;
            _attemptBuffer.DiscardAttempt();
        }

        private static void BindActiveQuests(bool discardAttempt = true)
        {
            // Подключаем выбранные Daily и все доступные Story.
            _dailyQuests = BindDefinitions(
                ResolveActiveDailyDefinitions());
            _storyQuests = BindDefinitions(
                QuestCatalog.StoryDefinitions);

            // Собираем единый список для обработки игровых событий.
            var activeQuests = new List<Quest>(
                _dailyQuests.Count + _storyQuests.Count);
            activeQuests.AddRange(_dailyQuests);
            activeQuests.AddRange(_storyQuests);
            _activeQuests = activeQuests.AsReadOnly();
            // Полная переинициализация завершает старую попытку.
            if (discardAttempt)
            {
                _attemptBuffer.DiscardAttempt();
            }
        }

        private static bool InitDailyQuestSet(DateTime localNow)
        {
            // Сохраняем прежний набор для очистки устаревшего прогресса.
            DailyQuestSetState savedState =
                GameDataManager.PlayerData.DailyQuestSet;
            bool hadGeneratedSet =
                !string.IsNullOrWhiteSpace(savedState?.GenerationDate);
            List<string> previousIds =
                savedState?.ActiveQuestIds?.ToList() ?? new List<string>();

            // Подключаем сохранённое состояние и создаём набор при необходимости.
            bool changed = _dailyQuestService.Init(
                QuestCatalog.DailyDefinitions,
                savedState,
                GameDataManager.PlayerData.QuestStates,
                localNow);
            GameDataManager.PlayerData.DailyQuestSet =
                _dailyQuestService.State;
            // Удаляем состояния квестов, покинувших активный набор.
            if (changed)
            {
                ApplyDailySetChange(previousIds, hadGeneratedSet);
            }

            return changed;
        }

        private static void ApplyDailySetChange(
            IReadOnlyCollection<string> previousIds,
            bool hadGeneratedSet)
        {
            var expiredIds = new HashSet<string>(
                hadGeneratedSet
                    ? previousIds.Where(questId =>
                        !_dailyQuestService.RetainsProgress(questId))
                    : QuestCatalog.DailyDefinitions.Select(
                        definition => definition.Id),
                StringComparer.Ordinal);

            GameDataManager.PlayerData.QuestStates.RemoveAll(
                quest => quest != null &&
                         expiredIds.Contains(quest.QuestId));
            GameDataManager.PlayerData.DailyQuestSet =
                _dailyQuestService.State;
        }

        private static IReadOnlyList<QuestDefinition>
            ResolveActiveDailyDefinitions()
        {
            var definitions = new List<QuestDefinition>(
                _dailyQuestService.State.ActiveQuestIds.Count);
            foreach (string questId in
                     _dailyQuestService.State.ActiveQuestIds)
            {
                if (!QuestCatalog.TryGet(
                        questId,
                        out QuestDefinition definition) ||
                    definition.Category != QuestCategory.Daily)
                {
                    throw new InvalidOperationException(
                        $"Дневной квест {questId} отсутствует в каталоге.");
                }

                definitions.Add(definition);
            }

            return definitions.AsReadOnly();
        }

        private static IReadOnlyList<Quest> BindDefinitions(
            IReadOnlyList<QuestDefinition> definitions)
        {
            var quests = new List<Quest>(definitions.Count);
            foreach (QuestDefinition definition in definitions)
            {
                Quest quest = GetOrCreateQuest(definition.Id);
                if (!_strategies.TryGetValue(
                        definition.Type,
                        out IQuestStrategy strategy))
                {
                    throw new InvalidOperationException(
                        $"Стратегия типа {definition.Type} не подключена.");
                }

                quest.Bind(definition, strategy);
                RestoreUniqueLevelProgress(quest);
                RestorePlayerStateProgress(quest);
                quests.Add(quest);
            }

            return quests.AsReadOnly();
        }

        private static void RestoreUniqueLevelProgress(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            if (!definition.CountUniqueLevels)
            {
                return;
            }

            LevelProgressOverview progressOverview =
                LevelManager.SavedProgressOverview;

            // Квест с заданной частью восстанавливаем из её агрегата.
            if (!string.IsNullOrWhiteSpace(definition.RequiredLocationId) &&
                !string.IsNullOrWhiteSpace(definition.RequiredPartOfDayId))
            {
                if (progressOverview.TryGetPart(
                        definition.RequiredLocationId,
                        definition.RequiredPartOfDayId,
                        out PartProgress part))
                {
                    RestoreUniqueLevelProgress(
                        quest,
                        definition,
                        part.Levels);
                }

                return;
            }

            // Остальные квесты восстанавливаем по всем игровым уровням.
            RestoreUniqueLevelProgress(
                quest,
                definition,
                progressOverview.Levels);
        }

        private static void RestoreUniqueLevelProgress(
            Quest quest,
            QuestDefinition definition,
            IReadOnlyList<LevelProgress> levels)
        {
            foreach (LevelProgress level in levels)
            {
                if (level.Stars < definition.RequiredStars)
                {
                    continue;
                }

                quest.Handle(
                    new LevelResultQuestEvent(
                        level.LevelNumber,
                        level.Stars,
                        level.Key.ToString(),
                        level.Key.LocationId,
                        level.Key.PartOfDayId));
            }
        }

        private static void RestorePlayerStateProgress(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            if (definition.Type != QuestType.PlayerState ||
                definition.StateId != PlayerStateIds.SkinOwned ||
                !int.TryParse(definition.EntityId, out int skinId) ||
                GameDataManager.PlayerData?.PurchasedSkinIds?.Contains(
                    skinId) != true)
            {
                return;
            }

            quest.Handle(
                new PlayerStateQuestEvent(
                    definition.StateId,
                    definition.EntityId,
                    1));
        }

        private static Quest GetOrCreateQuest(string questId)
        {
            GameDataManager.PlayerData.QuestStates ??=
                new List<Quest>();
            Quest quest = GameDataManager.PlayerData.QuestStates
                .FirstOrDefault(savedQuest =>
                    savedQuest.QuestId == questId);
            if (quest != null)
            {
                return quest;
            }

            quest = new Quest
            {
                QuestId = questId
            };
            GameDataManager.PlayerData.QuestStates.Add(quest);
            return quest;
        }

        private static void HandlePlayerDataReplaced()
        {
            if (_activeQuests.Count == 0)
            {
                return;
            }

            bool dailySetChanged = InitDailyQuestSet(DateTime.Now);
            BindActiveQuests();
            if (dailySetChanged)
            {
                PlayerProgressCommitter.Commit(
                    CheckpointReason.DailyQuestSetRotated);
                GameEventsManager.DailyQuestSetChanged();
            }

            foreach (Quest quest in _activeQuests)
            {
                GameEventsManager.QuestStateChanged(quest.Id);
            }
        }

        private static void HandleActionCounterQuestEvent(
            ActionCounterQuestEvent questEvent)
        {
            _attemptBuffer.Add(questEvent);
        }

        private static void HandleSkinPurchased(
            int skinId,
            ResourceType _,
            int __)
        {
            // Применяем постоянный факт сразу, без буфера попытки уровня.
            var questEvent = new PlayerStateQuestEvent(
                PlayerStateIds.SkinOwned,
                skinId.ToString(),
                1);
            foreach (Quest quest in _activeQuests)
            {
                bool wasCompleted = quest.IsCompleted;
                if (!quest.Handle(questEvent))
                {
                    continue;
                }

                if (!wasCompleted && quest.IsCompleted)
                {
                    GameEventsManager.QuestCompleted(quest.Id);
                }

                GameEventsManager.QuestStateChanged(quest.Id);
            }

            // SkinManager сохраняет покупку и обновлённый квест одним checkpoint после события.
        }

        private static void HandleLevelStarted(int _)
        {
            _attemptBuffer.StartAttempt();
        }

        private static void HandleLevelCompleted(int levelId, int stars)
        {
            IReadOnlyList<ActionCounterQuestEvent> bufferedEvents =
                _attemptBuffer.CompleteAttempt();
            bool hasProgressKey =
                LevelManager.TryGetCurrentProgressKey(
                    out LevelProgressKey progressKey);
            var levelResultEvent =
                new LevelResultQuestEvent(
                    levelId,
                    stars,
                    hasProgressKey ? progressKey.ToString() : string.Empty,
                    hasProgressKey ? progressKey.LocationId : string.Empty,
                    hasProgressKey ? progressKey.PartOfDayId : string.Empty);
            var changedQuests = new List<Quest>();
            var completedQuests = new List<Quest>();

            // Применяем факты завершённой попытки ко всем активным квестам.
            foreach (Quest quest in _activeQuests)
            {
                bool wasCompleted = quest.IsCompleted;
                bool progressChanged = quest.Handle(levelResultEvent);
                foreach (ActionCounterQuestEvent questEvent in bufferedEvents)
                {
                    progressChanged |= quest.Handle(questEvent);
                }

                if (!progressChanged)
                {
                    continue;
                }

                changedQuests.Add(quest);
                if (!wasCompleted && quest.IsCompleted)
                {
                    completedQuests.Add(quest);
                }
            }

            if (changedQuests.Count == 0)
            {
                return;
            }

            // Сохраняем все изменения попытки одним checkpoint.
            PlayerProgressCommitter.Commit(
                completedQuests.Count > 0
                    ? CheckpointReason.QuestCompleted
                    : CheckpointReason.QuestProgressed);

            // Уведомляем о завершении и любом изменённом состоянии.
            foreach (Quest quest in completedQuests)
            {
                GameEventsManager.QuestCompleted(quest.Id);
            }

            foreach (Quest quest in changedQuests)
            {
                GameEventsManager.QuestStateChanged(quest.Id);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Генерирует следующий набор Daily через штатный DailyQuestService.
        /// </summary>
        public static bool GenerateNextDailySetForTesting()
        {
            if (!_dailyQuestService.IsInitialized)
            {
                return false;
            }

            List<string> previousIds =
                _dailyQuestService.State.ActiveQuestIds.ToList();
            if (!_dailyQuestService.GenerateNextSetForTesting(
                    GameDataManager.PlayerData.QuestStates))
            {
                return false;
            }

            CompleteDailySetChange(previousIds);
            return true;
        }

        /// <summary>
        /// Сбрасывает выбранный активный квест для dev-тестирования.
        /// </summary>
        public static bool ResetQuestForTesting(string questId)
        {
            Quest quest = _activeQuests.FirstOrDefault(
                activeQuest => activeQuest.Id == questId);
            if (quest == null)
            {
                return false;
            }

            quest.Reset();
            _attemptBuffer.DiscardAttempt();
            GameDataManager.SaveData();
            GameEventsManager.QuestStateChanged(quest.Id);
            return true;
        }
#endif

        /// <summary>
        /// Выдаёт награду завершённого активного квеста один раз.
        /// </summary>
        public static bool ClaimReward(string questId)
        {
            Quest quest = _activeQuests.FirstOrDefault(
                activeQuest => activeQuest.Id == questId);
            if (quest == null || !quest.CanClaimReward)
            {
                return false;
            }

            bool rewardAdded = ResourceManager.AddResource(
                quest.RewardType,
                quest.RewardAmount);
            if (!rewardAdded)
            {
                return false;
            }

            if (quest.RewardType == ResourceType.Coins)
            {
                GameEventsManager.EarnCoins(quest.RewardAmount);
            }

            quest.MarkRewardClaimed();
            if (quest.Category == QuestCategory.Daily)
            {
                _playerExperienceService.GrantExperienceForClaimedDailyQuest(
                    GameDataManager.PlayerData);
            }
            else if (quest.Category == QuestCategory.Story)
            {
                _playerExperienceService
                    .GrantExperienceForClaimedStorylineQuest(
                        GameDataManager.PlayerData);
            }

            PlayerProgressCommitter.Commit(
                CheckpointReason.QuestRewardClaimed);

            GameEventsManager.QuestRewardReceived(questId);
            GameEventsManager.QuestStateChanged(questId);
            return true;
        }
    }
}
