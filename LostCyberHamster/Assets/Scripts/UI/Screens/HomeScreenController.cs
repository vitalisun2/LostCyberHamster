using System;
using System.Threading.Tasks;
using Assets.Scripts.Tutorial;
using GameManagement;
using GameManagement.Progress;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class HomeScreenController: ScreenController
    {
        private Button _buttonStart => _contentRoot.Q<Button>("btn_play");
        private Button _buttonSelectLevel => _contentRoot.Q<Button>("btn_select-level");
        private Button _buttonLeaderboard => _contentRoot.Q<Button>("btn_leaderboard");

        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");

        private Button _buttonCharacter => _contentRoot.Q<Button>("btn_character");
        private Button _buttonTutorial => _contentRoot.Q<Button>("btn_tutorial");

        private Button _buttonQuests => _contentRoot.Q<Button>("btn_quests");
        private Button _buttonTasks => _contentRoot.Q<Button>("btn_tasks");
        private Button _buttonSuperAttacks =>
            _contentRoot.Q<Button>("super-attacks__xp-button");
        private Button _buttonShop => _contentRoot.Q<Button>("btn_shop");

        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");
        private Label _playerLevel =>
            _contentRoot.Q<Label>("home__player-level");
        private ProgressBar _experienceProgress =>
            _contentRoot.Q<ProgressBar>("home__xp-progress");
        private Label _experienceLabel =>
            _contentRoot.Q<Label>("home__xp-label");


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
            SettingsScreenController.OpenFrom(ScreenEnum.HomeScreen);
        }

        private void OnClickBtnLeaderboard(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.LeaderboardScreen);
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonStart?.RegisterCallback<ClickEvent>(OnClickBtnStart);
            _buttonSelectLevel?.RegisterCallback<ClickEvent>(OnClickBtnSelectLevel);
            _buttonLeaderboard?.RegisterCallback<ClickEvent>(OnClickBtnLeaderboard);
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonCharacter?.RegisterCallback<ClickEvent>(OnClickBtnCharacter);
            _buttonTutorial?.RegisterCallback<ClickEvent>(OnClickBtnTutorial);
            _buttonQuests?.RegisterCallback<ClickEvent>(OnClickBtnQuests);
            _buttonTasks?.RegisterCallback<ClickEvent>(OnClickBtnTasks);
            _buttonSuperAttacks?.RegisterCallback<ClickEvent>(OnClickBtnSuperAttacks);
            _buttonShop?.RegisterCallback<ClickEvent>(OnClickBtnShop);
        }

        private void OnClickBtnSuperAttacks(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.SuperAttacksScreen);
        }

        private void OnClickBtnTasks(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.DailyTasksScreen);
        }


        private void OnClickBtnShop(ClickEvent evt)
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
            _buttonLeaderboard?.UnregisterCallback<ClickEvent>(OnClickBtnLeaderboard);
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonCharacter?.UnregisterCallback<ClickEvent>(OnClickBtnCharacter);
            _buttonTutorial?.UnregisterCallback<ClickEvent>(OnClickBtnTutorial);
            _buttonQuests?.UnregisterCallback<ClickEvent>(OnClickBtnQuests);
            _buttonTasks?.UnregisterCallback<ClickEvent>(OnClickBtnTasks);
            _buttonSuperAttacks?.UnregisterCallback<ClickEvent>(OnClickBtnSuperAttacks);
            _buttonShop?.UnregisterCallback<ClickEvent>(OnClickBtnShop);
        }

        protected override async Task OnLoadAsync()
        {
            if (_buttonHome != null)
            {
                _buttonHome.style.display = DisplayStyle.None;
            }

            RefreshExperiencePanel();
            await ChangeBackgroundAsync("HomeScreenSprite");
        }

        private void RefreshExperiencePanel()
        {
            var playerData = GameDataManager.PlayerData;
            if (playerData == null)
            {
                return;
            }

            var experienceThreshold =
                PlayerExperienceService.PlayerLevelThreshold;
            _playerLevel.text =
                $"{LocalizationManager.GetLocalizedString("super_attacks_level_short")} " +
                $"{playerData.PlayerLevel}";
            _experienceLabel.text =
                $"{playerData.ExperiencePoints} / {experienceThreshold}";
            _experienceProgress.lowValue = 0;
            _experienceProgress.highValue = experienceThreshold;
            _experienceProgress.value = playerData.ExperiencePoints;
            _experienceProgress.title =
                LocalizationManager.GetLocalizedString(
                    "super_attacks_xp_marker");
        }

    }

}
