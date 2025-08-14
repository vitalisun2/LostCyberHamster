using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameManagement;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.XR;

namespace Vues.GameCore
{
    public static class QuestManager
    {
        /// <summary>
        /// Pool of daily quests to generate from.
        /// </summary>
        private static List<Quest> _dailyTasksPool = new List<Quest>();

        public static List<Quest> DailyTasks = new List<Quest>();
        public static List<Quest> StorylineQuests = new List<Quest>();

        public static async Task Init()
        {
            var questData = await Addressables.LoadAssetAsync<TextAsset>("questData").Task;
            var quests = JsonUtility.FromJson<QuestData>(questData.text);

            StorylineQuests = quests.StorylineQuests.ToList();
            _dailyTasksPool = quests.DailyTasksPool.ToList();

            //GameRepository.GameData.PlayerData.DailyQuestRefreshDate = "2021-01-01";

            CheckAndRefreshDailyQuests();

            SetComplitedStoryLineQuests();
        }

        private static void SetComplitedStoryLineQuests()
        {
            var completedQuests = GameDataManager.PlayerData.ComplitedStorylineQuests;
            foreach (var quest in StorylineQuests)
            {
                if (completedQuests.Keys.Contains(quest.Id))
                {
                    quest.IsCompleted = true;
                    if (completedQuests[quest.Id])
                    {
                        quest.IsRewardRecieved = true;
                    }
                }
            }
        }

        public static void OnEnable()
        {
            GameEventsManager.OnCoinCollected += HandleCoinCollected;
            GameEventsManager.OnCrystalsCollected += HandleCrystallCollected;
            GameEventsManager.OnObstacleJumpedOver += HandleObstacleJumpedOver;
            GameEventsManager.OnObstacleJumpedOn += HandleObstacleJumpedOn;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnCoinCollected -= HandleCoinCollected;
            GameEventsManager.OnCrystalsCollected -= HandleCrystallCollected;
            GameEventsManager.OnObstacleJumpedOver -= HandleObstacleJumpedOver;
            GameEventsManager.OnObstacleJumpedOn -= HandleObstacleJumpedOn;
        }


        private static void HandleCoinCollected(int amount)
        {
            UpdateQuests(ActionTypeEnum.CollectCoins, amount);
        }

        private static void HandleCrystallCollected(int amount)
        {
            UpdateQuests(ActionTypeEnum.CollectCrystals, amount);
        }

        private static void HandleObstacleJumpedOver(string obstacleName)
        {
            UpdateQuests(ActionTypeEnum.JumpOverObstacles, 1, obstacleName);
        }

        private static void HandleObstacleJumpedOn(string obstacleName)
        {
            UpdateQuests(ActionTypeEnum.JumpOnObstacles, 1, obstacleName);
        }

        private static void UpdateQuests(ActionTypeEnum actionType, int progressAmount = 1, string objectName = "")
        {
            UpdateQuestList(DailyTasks, actionType, progressAmount);
            UpdateQuestList(StorylineQuests, actionType, progressAmount);
        }

        private static void UpdateQuestList(List<Quest> quests, ActionTypeEnum actionType, int progressAmount)
        {
            foreach (var quest in quests)
            {
                if (quest.ActionType == actionType && !quest.IsCompleted)
                {
                    quest.Progress(progressAmount);
                    if (quest.IsCompleted)
                    {
                        GameEventsManager.QuestCompleted(quest.Id);
                    }
                }
            }
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
                GameDataManager.SaveData();

                Debug.Log("Daily Tasks refreshed. Count: " + dailyTasks.Count);
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

        public static void GetReward(Quest quest)
        {
            if (!quest.IsCompleted)
                return;

            switch (quest.RewardTypeId)
            {
                case (int)ResourceType.Coins:
                    GameEventsManager.EarnCoins(quest.RewardAmount);
                    GameDataManager.PlayerData.Money += quest.RewardAmount;
                    break;
                case (int)ResourceType.Crystals:
                    GameDataManager.PlayerData.Crystals += quest.RewardAmount;
                    break;
                default:
                    break;
            }

            quest.IsRewardRecieved = true;
            GameEventsManager.QuestRewardRecieved(quest.Id);
        }
    }
}
