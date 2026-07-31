using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                    new LevelResultQuestStrategy()
            };

        private static readonly QuestAttemptBuffer _attemptBuffer = new();
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static Quest ActiveQuestForTesting =>
            _dailyQuests.FirstOrDefault();
#endif

        /// <summary>
        /// Загружает каталог и восстанавливает активные квесты.
        /// </summary>
        public static async Task Init()
        {
            await QuestCatalog.LoadAsync();
            if (QuestCatalog.DailyDefinitions.Count != 1 ||
                QuestCatalog.StoryDefinitions.Count != 1)
            {
                throw new InvalidOperationException(
                    "MVP-каталог должен содержать один дневной " +
                    "и один сюжетный квест.");
            }

            BindActiveQuests();
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
            GameDataManager.PlayerDataReplaced -= HandlePlayerDataReplaced;
            _attemptBuffer.DiscardAttempt();
        }

        private static void BindActiveQuests()
        {
            _dailyQuests = BindDefinitions(
                QuestCatalog.DailyDefinitions);
            _storyQuests = BindDefinitions(
                QuestCatalog.StoryDefinitions);

            var activeQuests = new List<Quest>(
                _dailyQuests.Count + _storyQuests.Count);
            activeQuests.AddRange(_dailyQuests);
            activeQuests.AddRange(_storyQuests);
            _activeQuests = activeQuests.AsReadOnly();
            _attemptBuffer.DiscardAttempt();
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
                quests.Add(quest);
            }

            return quests.AsReadOnly();
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

            BindActiveQuests();
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

        private static void HandleLevelStarted(int _)
        {
            _attemptBuffer.StartAttempt();
        }

        private static void HandleLevelCompleted(int levelId, int stars)
        {
            IReadOnlyList<ActionCounterQuestEvent> bufferedEvents =
                _attemptBuffer.CompleteAttempt();
            var levelResultEvent =
                new LevelResultQuestEvent(levelId, stars);
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
        /// Сбрасывает дневной MVP-квест для dev-тестирования.
        /// </summary>
        public static bool ResetActiveQuestForTesting()
        {
            Quest quest = ActiveQuestForTesting;
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
