using System.Collections.Generic;
using System;
using System.Linq;
using Assets.Scripts.Tutorial;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace LostCyberHamster.UI
{
    public class QuestsScreenController : ScreenController
    {
        private VisualElement _questsContainer =>
            _contentRoot.Q<VisualElement>("quests_container");
        private VisualElement _questsTabs =>
            _contentRoot.Q<VisualElement>("quests-tabs");
        private Button _buttonQuestsPrev =>
            _contentRoot.Q<Button>("btn__quests-prev");
        private Button _buttonQuestsNext =>
            _contentRoot.Q<Button>("btn__quests-next");
        private Button _buttonDailyTab =>
            _contentRoot.Q<Button>("btn__quests-tab-daily");
        private Button _buttonStoryTab =>
            _contentRoot.Q<Button>("btn__quests-tab-story");
        private bool _showDailyTasks = true;
        private int _currentQuestIndex;
        private bool _dailyAutoPresented;
        private Func<bool> _canPresentDailyReward;
        private Button DailyBonusButton => _contentRoot.Q<Button>("quests-daily-bonus");
        public void SetDailyRewardGate(Func<bool> canPresent) => _canPresentDailyReward = canPresent;
        private IVisualElementScheduledItem
            _dailyCommonRewardModalSchedule;

        private IReadOnlyList<Quest> ActiveQuests =>
            _showDailyTasks
                ? QuestManager.DailyQuests
                : QuestManager.StoryQuests;

        public QuestsScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.QuestsScreen;

        protected override string ScreenBackgroundAddress => "QuestsBackgroundSprite";

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return ScreenLayout.Fit(content.Q("quests-viewport"), content.Q("quests-scale-frame"),
                content.Q("questsscreen"), new Vector2(1844, 853));
        }

        protected override void BindView()
        {
            _dailyAutoPresented = false;
            // Незабранная первая Story открывается сразу, сохраняя свободное переключение вкладок.
            int morningIndex = QuestManager.StoryQuests.ToList().FindIndex(quest =>
                quest.Id == FirstSessionGoalPresenter.MorningQuestId && !quest.IsRewardClaimed);
            _showDailyTasks = morningIndex < 0;
            int pageSize = System.Math.Max(1, ConfigurationManager.Config.DisplayQuestsCount);
            _currentQuestIndex = morningIndex < 0 ? 0 : morningIndex / pageSize * pageSize;
            ApplyNextGoalTarget(pageSize);
            RenderActivePage();
            ScheduleDailyCommonRewardModal();
        }

        /// <summary>Открывает вкладку и страницу конкретного экземпляра из карточки цели.</summary>
        private void ApplyNextGoalTarget(int pageSize)
        {
            if (!NextGoalNavigation.TryConsume(ScreenEnum.QuestsScreen, out var goal)) return;

            // Общая Daily-награда находится на своей вкладке; онбординг сохраняет выбор первой Story.
            if (goal.Action == NextGoalAction.DailyReward)
            {
                _showDailyTasks = true;
                _currentQuestIndex = 0;
                return;
            }
            if (goal.Action != NextGoalAction.Quest) return;

            // Ищем именно предъявленный экземпляр, чтобы одинаковые задания разных дней не подменялись.
            int storyIndex = QuestManager.StoryQuests.ToList().FindIndex(quest =>
                string.Equals(quest.InstanceId, goal.Target, StringComparison.Ordinal));
            int dailyIndex = QuestManager.DailyQuests.ToList().FindIndex(quest =>
                string.Equals(quest.InstanceId, goal.Target, StringComparison.Ordinal));
            if (storyIndex < 0 && dailyIndex < 0) return;
            _showDailyTasks = storyIndex < 0;
            int index = _showDailyTasks ? dailyIndex : storyIndex;
            _currentQuestIndex = index / pageSize * pageSize;
        }

        private void RenderActivePage()
        {
            IReadOnlyList<Quest> quests = ActiveQuests;
            int pageSize = ConfigurationManager.Config.DisplayQuestsCount;
            _contentRoot.Q<VisualElement>("quests-content").EnableInClassList("quests-content--daily", _showDailyTasks);
            _contentRoot.Q("quests-daily-rule-panel").style.display = _showDailyTasks ? DisplayStyle.Flex : DisplayStyle.None;
            _contentRoot.Q("quests-daily-bonus-panel").style.display = _showDailyTasks ? DisplayStyle.Flex : DisplayStyle.None;
            int claimed = QuestManager.DailyQuests.Count(quest => quest.IsRewardClaimed);
            var bonus = QuestManager.GetDailyCommonReward();
            _contentRoot.Q<Label>("quests-daily-bonus-title").text = LocalizationManager.GetLocalizedString(
                bonus != null && claimed < 3 ? "retention_saved_bonus" : "retention_set_bonus");
            _contentRoot.Q<Label>("quests-daily-amount").text = (bonus?.Amount ?? QuestManager.DailyCommonRewardAmount).ToString();
            var more = _contentRoot.Q<Label>("quests-daily-more");
            more.text = bonus?.RemainingRewards > 0 ? string.Format(LocalizationManager.GetLocalizedString("retention_more_rewards"), bonus.RemainingRewards) : string.Empty;
            more.style.display = bonus?.RemainingRewards > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // Три отметки относятся к Claim текущего набора; отложенный приз показан отдельно.
            var marks = _contentRoot.Q("quests-daily-marks");
            marks.Clear();
            for (int i = 0; i < 3; i++)
            {
                var mark = new Label(i < claimed ? "✓" : "") { pickingMode = PickingMode.Ignore };
                mark.AddToClassList("quests-daily-mark");
                mark.EnableInClassList("quests-daily-mark--claimed", i < claimed);
                marks.Add(mark);
            }
            _contentRoot.Q<Label>("quests-daily-progress").text = string.Format(
                LocalizationManager.GetLocalizedString("retention_daily_claims"), claimed);
            DailyBonusButton.text = LocalizationManager.GetLocalizedString("retention_claim");
            DailyBonusButton.SetEnabled(bonus != null);

            // Нормализуем страницу после смены набора или количества квестов.
            if (quests.Count == 0 ||
                _currentQuestIndex >= quests.Count)
            {
                _currentQuestIndex = 0;
            }

            // Перестраиваем видимую страницу и состояние навигации.
            _questsContainer.Clear();
            foreach (Quest quest in quests
                         .Skip(_currentQuestIndex)
                         .Take(pageSize))
            {
                var item = new QuestItem(quest);
                if (quest.Id == FirstSessionGoalPresenter.MorningQuestId && !quest.IsRewardClaimed)
                {
                    item.AddToClassList("quest-card--first-session");
                    item.EnableInClassList("quest-card--first-session-claim", quest.CanClaimReward);
                }
                _questsContainer.Add(item);
            }

            DisplayStyle navigationDisplay =
                quests.Count > pageSize
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _buttonQuestsNext.style.display = navigationDisplay;
            _buttonQuestsPrev.style.display = navigationDisplay;

            // Переключаем цельный арт вкладок и раскладку набора.
            _questsTabs.EnableInClassList(
                "dual-tabs--left-active",
                _showDailyTasks);
            _questsTabs.EnableInClassList(
                "dual-tabs--right-active",
                !_showDailyTasks);
            _questsContainer.EnableInClassList(
                "quests-container--story",
                !_showDailyTasks);
        }

        protected override void OnSubscribeToEvents()
        {
            DailyBonusButton?.RegisterCallback<ClickEvent>(OnDailyBonusClicked);
            GameEventsManager.OnQuestStateChanged +=
                HandleQuestStateChanged;
            GameEventsManager.OnQuestRewardReceived +=
                HandleQuestRewardReceived;
            GameEventsManager.OnDailyQuestSetChanged +=
                HandleDailyQuestSetChanged;
            GameEventsManager.OnStoryQuestSetChanged +=
                HandleStoryQuestSetChanged;
            _buttonQuestsNext?.RegisterCallback<ClickEvent>(
                OnClickNextQuestPage);
            _buttonQuestsPrev?.RegisterCallback<ClickEvent>(
                OnClickPreviousQuestPage);
            _buttonDailyTab?.RegisterCallback<ClickEvent>(OnClickDailyTab);
            _buttonStoryTab?.RegisterCallback<ClickEvent>(OnClickStoryTab);
        }

        private void OnClickDailyTab(ClickEvent evt)
        {
            ShowTab(showDailyTasks: true);
        }

        private void OnClickStoryTab(ClickEvent evt)
        {
            ShowTab(showDailyTasks: false);
        }

        private void ShowTab(bool showDailyTasks)
        {
            if (_showDailyTasks == showDailyTasks)
            {
                return;
            }

            _showDailyTasks = showDailyTasks;
            if (_showDailyTasks) _dailyAutoPresented = false;
            _currentQuestIndex = 0;
            RenderActivePage();
            if (_showDailyTasks)
            {
                ScheduleDailyCommonRewardModal();
            }
            else
            {
                CancelDailyCommonRewardModal();
            }
        }

        private void OnClickNextQuestPage(ClickEvent evt)
        {
            IReadOnlyList<Quest> quests = ActiveQuests;
            _currentQuestIndex +=
                ConfigurationManager.Config.DisplayQuestsCount;
            if (_currentQuestIndex >= quests.Count)
            {
                _currentQuestIndex = 0;
            }

            RenderActivePage();
        }

        private void OnClickPreviousQuestPage(ClickEvent evt)
        {
            int pageSize = ConfigurationManager.Config.DisplayQuestsCount;
            _currentQuestIndex -= pageSize;
            if (_currentQuestIndex < 0)
            {
                _currentQuestIndex =
                    ((ActiveQuests.Count - 1) / pageSize) * pageSize;
            }

            RenderActivePage();
        }

        private void HandleQuestStateChanged(string questId)
        {
            if (!ActiveQuests.Any(quest => quest.Id == questId))
            {
                return;
            }

            _questsContainer?.schedule.Execute(RenderActivePage);
        }

        private void HandleQuestRewardReceived(string questId)
        {
            if (!QuestManager.DailyQuests.Any(
                    quest => quest.Id == questId))
            {
                return;
            }

            ScheduleDailyCommonRewardModal();
        }

        private void HandleDailyQuestSetChanged()
        {
            if (_showDailyTasks)
            {
                _questsContainer?.schedule.Execute(RenderActivePage);
                ScheduleDailyCommonRewardModal();
            }
        }

        private void HandleStoryQuestSetChanged()
        {
            if (!_showDailyTasks)
            {
                _questsContainer?.schedule.Execute(RenderActivePage);
            }
        }

        private void ScheduleDailyCommonRewardModal()
        {
            CancelDailyCommonRewardModal();
            if (_dailyAutoPresented || !_showDailyTasks ||
                !QuestManager.CanClaimDailyCommonReward)
            {
                return;
            }

            _dailyCommonRewardModalSchedule = _questsContainer.schedule
                .Execute(ShowDailyCommonRewardModal)
                .StartingIn(1000);
        }

        private void ShowDailyCommonRewardModal()
        {
            _dailyCommonRewardModalSchedule = null;
            if (!_showDailyTasks ||
                !QuestManager.CanClaimDailyCommonReward)
            {
                return;
            }

            if (_canPresentDailyReward?.Invoke() != true)
            {
                ScheduleDailyCommonRewardModal();
                return;
            }
            _dailyAutoPresented = true;

            UIManager.OnModalShow?.Invoke(
                ScreenEnum.DailyQuestRewardModal);
        }

        private void OnDailyBonusClicked(ClickEvent evt)
        {
            if (_canPresentDailyReward?.Invoke() == true) ShowDailyCommonRewardModal();
        }

        private void CancelDailyCommonRewardModal()
        {
            _dailyCommonRewardModalSchedule?.Pause();
            _dailyCommonRewardModalSchedule = null;
        }

        protected override void OnUnsubscribeFromEvents()
        {
            DailyBonusButton?.UnregisterCallback<ClickEvent>(OnDailyBonusClicked);
            CancelDailyCommonRewardModal();
            GameEventsManager.OnQuestStateChanged -=
                HandleQuestStateChanged;
            GameEventsManager.OnQuestRewardReceived -=
                HandleQuestRewardReceived;
            GameEventsManager.OnDailyQuestSetChanged -=
                HandleDailyQuestSetChanged;
            GameEventsManager.OnStoryQuestSetChanged -=
                HandleStoryQuestSetChanged;
            _buttonQuestsNext?.UnregisterCallback<ClickEvent>(
                OnClickNextQuestPage);
            _buttonQuestsPrev?.UnregisterCallback<ClickEvent>(
                OnClickPreviousQuestPage);
            _buttonDailyTab?.UnregisterCallback<ClickEvent>(
                OnClickDailyTab);
            _buttonStoryTab?.UnregisterCallback<ClickEvent>(
                OnClickStoryTab);
        }
    }
}
