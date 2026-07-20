using System;
using System.Threading.Tasks;
using Assets.Scripts.Tutorial;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class HomeScreenController: ScreenController
    {
        private Button _buttonStart => _contentRoot.Q<Button>("btn_play");
        private Button _buttonSelectLevel => _contentRoot.Q<Button>("btn_select-level");

        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");

        private Button _buttonCharacter => _contentRoot.Q<Button>("btn_character");
        private Button _buttonTutorial => _contentRoot.Q<Button>("btn_tutorial");

        private Button _buttonQuests => _contentRoot.Q<Button>("btn_quests");
        private Button _buttonTasks => _contentRoot.Q<Button>("btn_tasks");

        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");
        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;


        protected override ScreenEnum _screenAssetName => ScreenEnum.HomeScreen;


        public HomeScreenController(UIDocument uiDocument): base(uiDocument)
        {
        }

        private void OnClickBtnStart(ClickEvent evt)
        {
            // AudioManager.PlayDefaultButtonSound();
            SceneManager.LoadScene("Game");
        }

        private void OnClickBtnSelectLevel(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.SelectLevelScreen);
        }

        private void OnClickBtnSettings(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.SettingsModal);
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonStart?.RegisterCallback<ClickEvent>(OnClickBtnStart);
            _buttonSelectLevel?.RegisterCallback<ClickEvent>(OnClickBtnSelectLevel);
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonCharacter?.RegisterCallback<ClickEvent>(OnClickBtnCharacter);
            _buttonTutorial?.RegisterCallback<ClickEvent>(OnClickBtnTutorial);
            _buttonQuests?.RegisterCallback<ClickEvent>(OnClickBtnQuests);
            _buttonTasks?.RegisterCallback<ClickEvent>(OnClickBtnTasks);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
        }

        private void OnClickBtnTasks(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.DailyTasksScreen);
        }


        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }


        private void OnClickBtnQuests(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.QuestsScreen);
        }


        private void OnClickBtnCharacter(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.CharacterScreen);
        }

        private void OnClickBtnTutorial(ClickEvent evt)
        {
            TutorialLaunchService.StartReplayFromMenu();
            SceneManager.LoadScene("Game");
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _buttonStart?.UnregisterCallback<ClickEvent>(OnClickBtnStart);
            _buttonSelectLevel?.UnregisterCallback<ClickEvent>(OnClickBtnSelectLevel);
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonCharacter?.UnregisterCallback<ClickEvent>(OnClickBtnCharacter);
            _buttonTutorial?.UnregisterCallback<ClickEvent>(OnClickBtnTutorial);
            _buttonQuests?.UnregisterCallback<ClickEvent>(OnClickBtnQuests);
            _buttonTasks?.UnregisterCallback<ClickEvent>(OnClickBtnTasks);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
        }

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("HomeScreenSprite");

            _buttonHome.style.display = DisplayStyle.None;

        }

    }

}
