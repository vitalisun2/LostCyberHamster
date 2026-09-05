using System.Collections.Generic;
using System.Linq;
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
            var screen = content.Q<VisualElement>("questsscreen");
            return new ScreenLayout(screen, size =>
                screen.EnableInClassList("quests-screen--compact", size.y < 760f));
        }

        protected override void BindView()
        {
            _showDailyTasks = true;
            _currentQuestIndex = 0;
            RenderActivePage();
            ScheduleDailyCommonRewardModal();
        }

        private void RenderActivePage()
        {
            IReadOnlyList<Quest> quests = ActiveQuests;
            int pageSize = ConfigurationManager.Config.DisplayQuestsCount;

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
                _questsContainer.Add(new QuestItem(quest));
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
            if (!_showDailyTasks ||
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

            UIManager.OnModalShow?.Invoke(
                ScreenEnum.DailyQuestRewardModal);
        }

        private void CancelDailyCommonRewardModal()
        {
            _dailyCommonRewardModalSchedule?.Pause();
            _dailyCommonRewardModalSchedule = null;
        }

        protected override void OnUnsubscribeFromEvents()
        {
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
