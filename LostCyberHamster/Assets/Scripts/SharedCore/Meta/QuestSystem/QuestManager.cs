using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameManagement;
using GameManagement.Progress;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.XR;
using Vues.GameCore.Quests;

namespace Vues.GameCore
{
    public static class QuestManager
    {
        /// <summary>
        /// Pool of daily quests to generate from.
        /// </summary>
        private static List<Quest> _dailyTasksPool = new List<Quest>();
        private static QuestSystem _mvpQuestSystem;
        private static Quest _mvpQuestView;
        private static readonly PlayerExperienceService _playerExperienceService = new();

        public static List<Quest> DailyTasks = new List<Quest>();
        public static List<Quest> StorylineQuests = new List<Quest>();

        public static async Task Init()
        {
            var questData = await Addressables.LoadAssetAsync<TextAsset>("questData").Task;
            var quests = JsonUtility.FromJson<QuestData>(questData.text);

            StorylineQuests = quests.StorylineQuests.ToList();
            _dailyTasksPool = quests.DailyTasksPool.ToList();
            InitializeMvpQuest(quests.MvpQuest);

            RestoreStorylineQuestProgress();
        }

        private static void RestoreStorylineQuestProgress()
        {
            var savedProgress = GameDataManager.PlayerData.StorylineQuestProgress ?? new List<StorylineQuestProgressEntry>();
            foreach (var quest in StorylineQuests)
            {
                var entry = savedProgress.FirstOrDefault(candidate => candidate.QuestId == quest.Id);
                if (entry != null)
                {
                    quest.IsCompleted = true;
                    quest.IsRewardRecieved = entry.IsRewardClaimed;
                }
            }
        }

        public static void OnEnable()
        {
            GameEventsManager.OnActionQuestEvent +=
                HandleActionQuestEvent;
            GameEventsManager.OnCoinCollected += HandleCoinCollected;
            GameEventsManager.OnCrystalsCollected += HandleCrystallCollected;
            GameEventsManager.OnObstacleJumpedOn += HandleObstacleJumpedOn;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnActionQuestEvent -=
                HandleActionQuestEvent;
            GameEventsManager.OnCoinCollected -= HandleCoinCollected;
            GameEventsManager.OnCrystalsCollected -= HandleCrystallCollected;
            GameEventsManager.OnObstacleJumpedOn -= HandleObstacleJumpedOn;
        }

        private static void InitializeMvpQuest(
            QuestDefinition definition)
        {
            // Запускаем новое ядро на сохранённом состоянии игрока.
            GameDataManager.PlayerData.BasicQuest ??= new QuestState();
            _mvpQuestSystem = new QuestSystem(
                definition,
                GameDataManager.PlayerData.BasicQuest,
                new ActionCounterQuestStrategy());

            // Собираем временную модель для существующей карточки и награды.
            Quest rewardTemplate = _dailyTasksPool.FirstOrDefault(
                quest => quest.Id == definition.Id);
            Quest savedQuest = GameDataManager.PlayerData.DailyTasks?
                .FirstOrDefault(quest => quest.Id == definition.Id);
            _mvpQuestView = new Quest
            {
                Id = definition.Id,
                Title = definition.Title,
                TargetAmount = definition.TargetAmount,
                RewardTypeId = rewardTemplate?.RewardTypeId ?? 0,
                RewardAmount = rewardTemplate?.RewardAmount ?? 0,
                ActionTypeString =
                    ActionTypeEnum.JumpOverObstacles.ToString(),
                IsRewardRecieved =
                    savedQuest?.IsRewardRecieved ?? false
            };
            SyncMvpQuestView();
            DailyTasks = new List<Quest> { _mvpQuestView };
            GameDataManager.PlayerData.DailyTasks = DailyTasks;
        }

        private static void HandleCoinCollected(int amount)
        {
            UpdateQuests(ActionTypeEnum.CollectCoins, amount);
        }

        private static void HandleCrystallCollected(int amount)
        {
            UpdateQuests(ActionTypeEnum.CollectCrystals, amount);
        }

        private static void HandleActionQuestEvent(
            ActionQuestEvent questEvent)
        {
            if (_mvpQuestSystem == null)
            {
                return;
            }

            // Новое ядро меняет сохранённое состояние только для своего action ID.
            bool wasCompleted = _mvpQuestSystem.State.IsCompleted;
            if (!_mvpQuestSystem.Handle(questEvent))
            {
                return;
            }

            // Старый UI получает актуальный снимок без своей квестовой логики.
            SyncMvpQuestView();
            if (!wasCompleted && _mvpQuestSystem.State.IsCompleted)
            {
                GameEventsManager.QuestCompleted(
                    _mvpQuestSystem.Definition.Id);
                PlayerProgressCommitter.Commit(
                    CheckpointReason.DailyQuestCompleted);
            }

            GameEventsManager.QuestStateChanged(
                _mvpQuestSystem.Definition.Id);
        }

        private static void HandleObstacleJumpedOn(string obstacleName)
        {
            UpdateQuests(ActionTypeEnum.JumpOnObstacles, 1, obstacleName);
        }

        private static void SyncMvpQuestView()
        {
            if (_mvpQuestSystem == null || _mvpQuestView == null)
            {
                return;
            }

            _mvpQuestView.CurrentAmount =
                _mvpQuestSystem.State.CurrentProgress;
            _mvpQuestView.IsCompleted =
                _mvpQuestSystem.State.IsCompleted;
        }

        private static void UpdateQuests(ActionTypeEnum actionType, int progressAmount = 1, string objectName = "")
        {
            bool dailyCompleted = UpdateQuestList(DailyTasks, actionType, progressAmount, isStoryline: false);
            bool storylineCompleted = UpdateQuestList(StorylineQuests, actionType, progressAmount, isStoryline: true);

            if (storylineCompleted)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.StorylineQuestCompleted);
            }
            else if (dailyCompleted)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.DailyQuestCompleted);
            }
        }

        private static bool UpdateQuestList(
            List<Quest> quests,
            ActionTypeEnum actionType,
            int progressAmount,
            bool isStoryline)
        {
            bool completed = false;

            foreach (var quest in quests)
            {
                if (quest.ActionType == actionType && !quest.IsCompleted)
                {
                    quest.Progress(progressAmount);
                    GameEventsManager.QuestStateChanged(quest.Id);
                    if (quest.IsCompleted)
                    {
                        if (isStoryline)
                        {
                            GetOrCreateStorylineProgress(quest.Id);
                        }

                        GameEventsManager.QuestCompleted(quest.Id);
                        completed = true;
                    }
                }
            }

            return completed;
        }


        public static void CheckAndRefreshDailyQuests()
        {
            string lastRefreshDate = GameDataManager.PlayerData.DailyTasksRefreshDate;
            string currentDate = System.DateTime.Now.ToString("yyyy-MM-dd");

            if (lastRefreshDate != currentDate)
            {
                var dailyTasks = GenerateDailyQuests(ConfigurationManager.Config.DailyTasksCount);
                GameDataManager.PlayerData.DailyTasksRefreshDate = currentDate;
                GameDataManager.PlayerData.DailyTasks = dailyTasks;
                PlayerProgressCommitter.Commit(CheckpointReason.QuestListRefreshed);
            }

            DailyTasks = GameDataManager.PlayerData.DailyTasks;
        }

        public static List<Quest> GenerateDailyQuests(int count)
        {
            List<Quest> dailyQuests = new List<Quest>();

            if (_dailyTasksPool.Count < count)
            {
                Debug.LogError("Not enough tasks in the pool to generate daily quests.");
                return dailyQuests;
            }

            for (int i = 0; i < count; i++)
            {
                var randomTask = _dailyTasksPool[Random.Range(0, _dailyTasksPool.Count)];

                if (dailyQuests.Select(x => x.Id).Contains(randomTask.Id))
                {
                    i--;
                    continue;
                }

                dailyQuests.Add(new Quest
                {
                    Id = randomTask.Id,
                    Title = randomTask.Title,
                    Description = randomTask.Description,
                    TargetAmount = randomTask.TargetAmount,
                    CurrentAmount = 0,
                    RewardTypeId = randomTask.RewardTypeId,
                    RewardAmount = randomTask.RewardAmount,
                    ActionTypeString = randomTask.ActionTypeString
                });
            }

            return dailyQuests;
        }

        public static bool GetReward(Quest quest)
        {
            if (!quest.IsCompleted || quest.IsRewardRecieved || quest.RewardAmount <= 0)
                return false;

            var isStoryline = StorylineQuests.Contains(quest);
            var isDaily = DailyTasks.Contains(quest);
            bool rewardAdded;
            switch (quest.RewardTypeId)
            {
                case (int)ResourceType.Coins:
                    rewardAdded = ResourceManager.AddResource(ResourceType.Coins, quest.RewardAmount);
                    if (!rewardAdded)
                    {
                        return false;
                    }

                    GameEventsManager.EarnCoins(quest.RewardAmount);
                    break;
                case (int)ResourceType.Crystals:
                    rewardAdded = ResourceManager.AddResource(ResourceType.Crystals, quest.RewardAmount);
                    if (!rewardAdded)
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }

            quest.IsRewardRecieved = true;
            if (isStoryline)
            {
                GetOrCreateStorylineProgress(quest.Id).IsRewardClaimed = true;
                _playerExperienceService
                    .GrantExperienceForClaimedStorylineQuest(
                        GameDataManager.PlayerData);
            }
            else if (isDaily)
            {
                _playerExperienceService
                    .GrantExperienceForClaimedDailyQuest(
                        GameDataManager.PlayerData);
            }

            GameEventsManager.QuestRewardRecieved(quest.Id);
            GameEventsManager.QuestStateChanged(quest.Id);
            PlayerProgressCommitter.Commit(CheckpointReason.QuestRewardClaimed);
            return true;
        }

        private static StorylineQuestProgressEntry GetOrCreateStorylineProgress(string questId)
        {
            GameDataManager.PlayerData.StorylineQuestProgress ??= new List<StorylineQuestProgressEntry>();

            var entry = GameDataManager.PlayerData.StorylineQuestProgress
                .FirstOrDefault(candidate => candidate.QuestId == questId);
            if (entry != null)
            {
                return entry;
            }

            entry = new StorylineQuestProgressEntry
            {
                QuestId = questId
            };
            GameDataManager.PlayerData.StorylineQuestProgress.Add(entry);
            return entry;
        }
    }
}
