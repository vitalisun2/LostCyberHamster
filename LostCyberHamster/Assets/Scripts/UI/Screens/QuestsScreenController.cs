using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class QuestsScreenController : ScreenController
    {
        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");

        private VisualElement _questsContainer => _contentRoot.Q<VisualElement>("quests_container");

        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;

        private Button _buttonQuestsPrev => _contentRoot.Q<Button>("btn__quests-prev");
        private Button _buttonQuestsNext => _contentRoot.Q<Button>("btn__quests-next");

        public QuestsScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }


        protected override ScreenEnum _screenAssetName => ScreenEnum.QuestsScreen;

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            await Init();
        }

        private async Task Init()
        {
            _questsContainer.Clear();
            if(QuestManager.StorylineQuests.Count <= ConfigurationManager.Config.DisplayQuestsCount)
            {
                _buttonQuestsNext.style.display = DisplayStyle.None;
                _buttonQuestsPrev.style.display = DisplayStyle.None;
            }
            foreach (var quest in QuestManager.StorylineQuests.Take(ConfigurationManager.Config.DisplayQuestsCount))
            {
                var questItem = new QuestItem(quest);
                _questsContainer.Add(questItem);
            }
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
        }

        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }


        private void OnClickBtnSettings(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.SettingsModal);
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
        }
    }
}