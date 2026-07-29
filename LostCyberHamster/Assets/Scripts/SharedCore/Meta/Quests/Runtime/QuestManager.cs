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
    /// Связывает активный квест с событиями игры, сейвом и UI.
    /// </summary>
    public static class QuestManager
    {
        private static QuestSystem _activeQuest;
        private static IReadOnlyList<QuestViewData> _dailyQuests =
            Array.Empty<QuestViewData>();
        private static readonly QuestAttemptBuffer _attemptBuffer = new();
        private static readonly PlayerExperienceService
            _playerExperienceService = new();

        public static IReadOnlyList<QuestViewData> DailyQuests =>
            _dailyQuests;

        public static IReadOnlyList<QuestViewData> StoryQuests { get; } =
            Array.Empty<QuestViewData>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static QuestDefinition ActiveDefinitionForTesting =>
            _activeQuest?.Definition;

        public static QuestState ActiveStateForTesting =>
            _activeQuest?.State;

        public static QuestViewData ActiveViewForTesting =>
            _dailyQuests.Count == 1
                ? _dailyQuests[0]
                : null;
#endif

        public static async Task Init()
        {
            await QuestCatalog.LoadAsync();
            if (QuestCatalog.DailyDefinitions.Count != 1)
            {
                throw new InvalidOperationException(
                    "MVP-каталог должен содержать один дневной квест.");
            }

            BindActiveQuest(QuestCatalog.DailyDefinitions[0]);
        }

        public static void OnEnable()
        {
            GameEventsManager.OnLevelStarted += HandleLevelStarted;
            GameEventsManager.OnActionQuestEvent +=
                HandleActionQuestEvent;
            GameEventsManager.OnLevelCompleted += HandleLevelCompleted;
            GameDataManager.PlayerDataReplaced += HandlePlayerDataReplaced;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnLevelStarted -= HandleLevelStarted;
            GameEventsManager.OnActionQuestEvent -=
                HandleActionQuestEvent;
            GameEventsManager.OnLevelCompleted -= HandleLevelCompleted;
            GameDataManager.PlayerDataReplaced -= HandlePlayerDataReplaced;
            _attemptBuffer.DiscardAttempt();
        }

        private static void BindActiveQuest(
            QuestDefinition definition)
        {
            QuestState state = GetOrCreateState(definition.Id);
            _activeQuest = new QuestSystem(
                definition,
                state,
                new ActionCounterQuestStrategy());
            _attemptBuffer.DiscardAttempt();
            RefreshView();
        }

        private static QuestState GetOrCreateState(string questId)
        {
            GameDataManager.PlayerData.QuestStates ??=
                new List<QuestState>();
            QuestState state = GameDataManager.PlayerData.QuestStates
                .FirstOrDefault(savedState =>
                    savedState.QuestId == questId);
            if (state != null)
            {
                return state;
            }

            state = new QuestState
            {
                QuestId = questId
            };
            GameDataManager.PlayerData.QuestStates.Add(state);
            return state;
        }

        private static void HandlePlayerDataReplaced()
        {
            if (_activeQuest == null)
            {
                return;
            }

            string questId = _activeQuest.Definition.Id;
            BindActiveQuest(_activeQuest.Definition);
            GameEventsManager.QuestStateChanged(questId);
        }

        private static void HandleActionQuestEvent(
            ActionQuestEvent questEvent)
        {
            _attemptBuffer.Add(questEvent);
        }

        private static void HandleLevelStarted(int _)
        {
            _attemptBuffer.StartAttempt();
        }

        private static void HandleLevelCompleted(int _, int __)
        {
            IReadOnlyList<ActionQuestEvent> bufferedEvents =
                _attemptBuffer.CompleteAttempt();
            if (_activeQuest == null || bufferedEvents.Count == 0)
            {
                return;
            }

            bool wasCompleted = _activeQuest.State.IsCompleted;
            bool progressChanged = false;
            foreach (ActionQuestEvent questEvent in bufferedEvents)
            {
                progressChanged |= _activeQuest.Handle(questEvent);
            }

            if (!progressChanged)
            {
                return;
            }

            RefreshView();
            bool questCompleted =
                !wasCompleted && _activeQuest.State.IsCompleted;
            PlayerProgressCommitter.Commit(
                questCompleted
                    ? CheckpointReason.DailyQuestCompleted
                    : CheckpointReason.DailyQuestProgressed);

            if (questCompleted)
            {
                GameEventsManager.QuestCompleted(
                    _activeQuest.Definition.Id);
            }

            GameEventsManager.QuestStateChanged(
                _activeQuest.Definition.Id);
        }

        private static void RefreshView()
        {
            _dailyQuests = _activeQuest == null
                ? Array.Empty<QuestViewData>()
                : new[]
                {
                    new QuestViewData(
                        _activeQuest.Definition,
                        _activeQuest.State)
                };
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool ResetActiveQuestForTesting()
        {
            if (_activeQuest == null)
            {
                return false;
            }

            QuestState state = _activeQuest.State;
            state.QuestId = _activeQuest.Definition.Id;
            state.CurrentProgress = 0;
            state.IsCompleted = false;
            state.IsRewardClaimed = false;
            _attemptBuffer.DiscardAttempt();
            RefreshView();
            GameDataManager.SaveData();
            GameEventsManager.QuestStateChanged(
                _activeQuest.Definition.Id);
            return true;
        }
#endif

        public static bool ClaimReward(string questId)
        {
            if (_activeQuest == null ||
                questId != _activeQuest.Definition.Id ||
                !_activeQuest.State.IsCompleted ||
                _activeQuest.State.IsRewardClaimed)
            {
                return false;
            }

            QuestDefinition definition = _activeQuest.Definition;
            bool rewardAdded = ResourceManager.AddResource(
                definition.RewardType,
                definition.RewardAmount);
            if (!rewardAdded)
            {
                return false;
            }

            if (definition.RewardType == ResourceType.Coins)
            {
                GameEventsManager.EarnCoins(
                    definition.RewardAmount);
            }

            _activeQuest.State.IsRewardClaimed = true;
            _playerExperienceService
                .GrantExperienceForClaimedDailyQuest(
                    GameDataManager.PlayerData);
            RefreshView();
            PlayerProgressCommitter.Commit(
                CheckpointReason.QuestRewardClaimed);

            GameEventsManager.QuestRewardReceived(questId);
            GameEventsManager.QuestStateChanged(questId);
            return true;
        }
    }
}
