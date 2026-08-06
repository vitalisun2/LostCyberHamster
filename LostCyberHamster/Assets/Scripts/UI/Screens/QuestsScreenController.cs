using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace LostCyberHamster.UI
{
    public class QuestsScreenController : ScreenController
    {
        private Button _buttonSettings =>
            _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome =>
            _contentRoot.Q<Button>("btn_home");
        private VisualElement _questsContainer =>
            _contentRoot.Q<VisualElement>("quests_container");
        private Button _buttonAddMoney =>
            _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals =>
            _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;
        private Button _buttonQuestsPrev =>
            _contentRoot.Q<Button>("btn__quests-prev");
        private Button _buttonQuestsNext =>
            _contentRoot.Q<Button>("btn__quests-next");
        private Button _buttonDailyTab =>
            _contentRoot.Q<Button>("btn__quests-tab-daily");
        private Button _buttonStoryTab =>
            _contentRoot.Q<Button>("btn__quests-tab-story");
        private VisualElement _dailyCommonReward =>
            _contentRoot.Q<VisualElement>("daily-common-reward");
        private Label _dailyCommonRewardProgress =>
            _contentRoot.Q<Label>("daily-common-reward__progress");
        private VisualElement _dailyCommonRewardAction =>
            _contentRoot.Q<VisualElement>("daily-common-reward__action");
        private Button _buttonDailyCommonRewardClaim =>
            _contentRoot.Q<Button>("daily-common-reward__claim");
        private Label _dailyCommonRewardAmount =>
            _contentRoot.Q<Label>("daily-common-reward__amount");
        private VisualElement _dailyCommonRewardImage =>
            _contentRoot.Q<VisualElement>("daily-common-reward__image");
        private VisualElement _dailyCommonRewardClaimed =>
            _contentRoot.Q<VisualElement>("daily-common-reward__claimed");

        private bool _showDailyTasks = true;
        private int _currentQuestIndex;

        private IReadOnlyList<Quest> ActiveQuests =>
            _showDailyTasks
                ? QuestManager.DailyQuests
                : QuestManager.StoryQuests;

        public QuestsScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.QuestsScreen;

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            _showDailyTasks = true;
            _currentQuestIndex = 0;
            RenderActivePage();
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

            // Обновляем активную вкладку и связанный с ней общий бонус.
            _buttonDailyTab.EnableInClassList(
                "quests-tab--active",
                _showDailyTasks);
            _buttonStoryTab.EnableInClassList(
                "quests-tab--active",
                !_showDailyTasks);
            RenderDailyCommonReward();
        }

        private void RenderDailyCommonReward()
        {
            // Общая награда относится только к Daily-вкладке.
            _dailyCommonReward.style.display = _showDailyTasks
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!_showDailyTasks)
            {
                return;
            }

            // Показываем актуальный прогресс и конфигурацию награды.
            _dailyCommonRewardProgress.text = FormatLocalized(
                "quests_daily_common_reward_progress",
                QuestManager.DailyCommonRewardCompletedCount,
                QuestManager.DailyQuestCount);
            _dailyCommonRewardAmount.text =
                QuestManager.DailyCommonRewardAmount.ToString();
            _dailyCommonRewardImage.style.backgroundImage =
                new StyleBackground(ResourceUIHelper.GetResourceImage(
                    QuestManager.DailyCommonRewardType));

            // После получения заменяем CTA постоянным статусом.
            bool isClaimed = QuestManager.IsDailyCommonRewardClaimed;
            bool canClaim = QuestManager.CanClaimDailyCommonReward;
            _dailyCommonRewardAction.style.display = isClaimed
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _dailyCommonRewardClaimed.style.display = isClaimed
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _buttonDailyCommonRewardClaim.SetEnabled(canClaim);
            _dailyCommonRewardAction.EnableInClassList(
                "quest-reward--disabled",
                !canClaim);
        }

        protected override void OnSubscribeToEvents()
        {
            GameEventsManager.OnQuestStateChanged +=
                HandleQuestStateChanged;
            GameEventsManager.OnDailyQuestSetChanged +=
                HandleDailyQuestSetChanged;
            GameEventsManager.OnStoryQuestSetChanged +=
                HandleStoryQuestSetChanged;
            GameEventsManager.OnDailyQuestCommonRewardChanged +=
                HandleDailyCommonRewardChanged;
            _buttonSettings?.RegisterCallback<ClickEvent>(
                OnClickBtnSettings);
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(
                OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(
                OnClickBtnAddMoney);
            _buttonQuestsNext?.RegisterCallback<ClickEvent>(
                OnClickNextQuestPage);
            _buttonQuestsPrev?.RegisterCallback<ClickEvent>(
                OnClickPreviousQuestPage);
            _buttonDailyTab?.RegisterCallback<ClickEvent>(OnClickDailyTab);
            _buttonStoryTab?.RegisterCallback<ClickEvent>(OnClickStoryTab);
            _buttonDailyCommonRewardClaim?.RegisterCallback<ClickEvent>(
                OnClickDailyCommonRewardClaim);
        }

        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        private void OnClickBtnSettings(ClickEvent evt)
        {
            SettingsScreenController.OpenFrom(ScreenEnum.QuestsScreen);
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }

        private void OnClickDailyTab(ClickEvent evt)
        {
            ShowTab(showDailyTasks: true);
        }

        private void OnClickStoryTab(ClickEvent evt)
        {
            ShowTab(showDailyTasks: false);
        }

        private void OnClickDailyCommonRewardClaim(ClickEvent evt)
        {
            QuestManager.ClaimDailyCommonReward();
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

        private void HandleDailyQuestSetChanged()
        {
            if (_showDailyTasks)
            {
                _questsContainer?.schedule.Execute(RenderActivePage);
            }
        }

        private void HandleStoryQuestSetChanged()
        {
            if (!_showDailyTasks)
            {
                _questsContainer?.schedule.Execute(RenderActivePage);
            }
        }

        private void HandleDailyCommonRewardChanged()
        {
            if (_showDailyTasks)
            {
                _dailyCommonReward?.schedule.Execute(
                    RenderDailyCommonReward);
            }
        }

        protected override void OnUnsubscribeFromEvents()
        {
            GameEventsManager.OnQuestStateChanged -=
                HandleQuestStateChanged;
            GameEventsManager.OnDailyQuestSetChanged -=
                HandleDailyQuestSetChanged;
            GameEventsManager.OnStoryQuestSetChanged -=
                HandleStoryQuestSetChanged;
            GameEventsManager.OnDailyQuestCommonRewardChanged -=
                HandleDailyCommonRewardChanged;
            _buttonSettings?.UnregisterCallback<ClickEvent>(
                OnClickBtnSettings);
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(
                OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(
                OnClickBtnAddMoney);
            _buttonQuestsNext?.UnregisterCallback<ClickEvent>(
                OnClickNextQuestPage);
            _buttonQuestsPrev?.UnregisterCallback<ClickEvent>(
                OnClickPreviousQuestPage);
            _buttonDailyTab?.UnregisterCallback<ClickEvent>(
                OnClickDailyTab);
            _buttonStoryTab?.UnregisterCallback<ClickEvent>(
                OnClickStoryTab);
            _buttonDailyCommonRewardClaim?.UnregisterCallback<ClickEvent>(
                OnClickDailyCommonRewardClaim);
        }

        private static string FormatLocalized(
            string localizationKey,
            params object[] arguments)
        {
            // Получаем локализованный шаблон с fallback на ключ.
            string template = LocalizationManager.GetLocalizedString(
                localizationKey) ?? localizationKey;
            // Ошибка контентного шаблона не должна ломать экран.
            try
            {
                return string.Format(template, arguments);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }
}
