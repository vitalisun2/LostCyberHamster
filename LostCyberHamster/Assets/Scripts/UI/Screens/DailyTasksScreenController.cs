using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class DailyTasksScreenController : ScreenController
    {
        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");

        private VisualElement _questsContainer => _contentRoot.Q<VisualElement>("quests_container");

        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;

        private Button _buttonQuestsPrev => _contentRoot.Q<Button>("btn__quests-prev");
        private Button _buttonQuestsNext => _contentRoot.Q<Button>("btn__quests-next");

        private int _currentQuestIndex = 0;

        public DailyTasksScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }


        protected override ScreenEnum _screenAssetName => ScreenEnum.DailyTasksScreen;

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            await Init();
        }

        private async Task Init()
        {
            _questsContainer.Clear();

            UpdateButtonVisibility();

            var questsToDisplay = QuestManager.DailyTasks
            .Skip(_currentQuestIndex)
            .Take(ConfigurationManager.Config.DisplayQuestsCount);

            foreach (var quest in questsToDisplay)
            {
                var questItem = new QuestItem(quest);
                _questsContainer.Add(questItem);
            }
        }

        private void UpdateButtonVisibility()
        {
            // Show/hide Next button
            _buttonQuestsNext.style.display = QuestManager.DailyTasks.Count > ConfigurationManager.Config.DisplayQuestsCount
                ? DisplayStyle.Flex : DisplayStyle.None;

            // Show/hide Prev button
            _buttonQuestsPrev.style.display = QuestManager.DailyTasks.Count > ConfigurationManager.Config.DisplayQuestsCount
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private async void OnNextButtonClicked()
        {
            _currentQuestIndex += ConfigurationManager.Config.DisplayQuestsCount;
            if (_currentQuestIndex >= QuestManager.DailyTasks.Count)
            {
                _currentQuestIndex = 0;
            }
            await Init();
        }

        // Prev button click handler
        private async void OnPrevButtonClicked()
        {
            _currentQuestIndex -= ConfigurationManager.Config.DisplayQuestsCount;

            // Wrap around to the end if we go below 0
            if (_currentQuestIndex < 0)
            {
                _currentQuestIndex = QuestManager.DailyTasks.Count -
                                     (QuestManager.DailyTasks.Count % ConfigurationManager.Config.DisplayQuestsCount == 0
                                         ? ConfigurationManager.Config.DisplayQuestsCount
                                         : QuestManager.DailyTasks.Count % ConfigurationManager.Config.DisplayQuestsCount);
            }

            await Init();
        }

        protected override void OnSubscribeToEvents()
        {
            GameEventsManager.OnQuestStateChanged +=
                HandleQuestStateChanged;
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonQuestsNext?.RegisterCallback<ClickEvent>(evt => OnNextButtonClicked());
            _buttonQuestsPrev?.RegisterCallback<ClickEvent>(evt => OnPrevButtonClicked());
        }

        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }


        private void OnClickBtnSettings(ClickEvent evt)
        {
            SettingsScreenController.OpenFrom(ScreenEnum.DailyTasksScreen);
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }

        private void HandleQuestStateChanged(string questId)
        {
            if (!QuestManager.DailyTasks.Any(
                    quest => quest.Id == questId))
            {
                return;
            }

            // Обновляем карточку после завершения текущего UI-события.
            _questsContainer?.schedule.Execute(() => _ = Init());
        }

        protected override void OnUnsubscribeFromEvents()
        {
            GameEventsManager.OnQuestStateChanged -=
                HandleQuestStateChanged;
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonQuestsNext?.UnregisterCallback<ClickEvent>(evt => OnNextButtonClicked());
            _buttonQuestsPrev?.UnregisterCallback<ClickEvent>(evt => OnPrevButtonClicked());
        }
    }
}
