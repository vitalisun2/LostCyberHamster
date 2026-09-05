using System;
using GameManagement;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class HomeScreenController: ScreenController
    {
        private Button _buttonStart => _contentRoot.Q<Button>("btn_play");
        private Button _buttonSelectLevel => _contentRoot.Q<Button>("btn_select-level");
        private Button _buttonLeaderboard => _contentRoot.Q<Button>("btn_leaderboard");

        private Button _buttonCharacter => _contentRoot.Q<Button>("btn_character");

        private Button _buttonQuests => _contentRoot.Q<Button>("btn_quests");
        private Button _buttonDevelopment =>
            _contentRoot.Q<Button>("btn_development");
        private Button _buttonShop => _contentRoot.Q<Button>("btn_shop");
        private Label _playerLevel =>
            _contentRoot.Q<Label>("home__player-level");
        private VisualElement _experienceFill =>
            _contentRoot.Q<VisualElement>("home__xp-fill");
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

        private void OnClickBtnLeaderboard(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.LeaderboardScreen);
        }

        protected override void OnSubscribeToEvents()
        {
            // Подключаем действия всех кнопок Home.
            _buttonStart?.RegisterCallback<ClickEvent>(OnClickBtnStart);
            _buttonSelectLevel?.RegisterCallback<ClickEvent>(OnClickBtnSelectLevel);
            _buttonLeaderboard?.RegisterCallback<ClickEvent>(OnClickBtnLeaderboard);
            _buttonCharacter?.RegisterCallback<ClickEvent>(OnClickBtnCharacter);
            _buttonQuests?.RegisterCallback<ClickEvent>(OnClickBtnQuests);
            _buttonDevelopment?.RegisterCallback<ClickEvent>(OnClickBtnDevelopment);
            _buttonShop?.RegisterCallback<ClickEvent>(OnClickBtnShop);
        }

        private void OnClickBtnDevelopment(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.CharacterDevelopmentScreen);
        }

        private void OnClickBtnShop(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.ShopScreen);
        }

        private void OnClickBtnQuests(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.QuestsScreen);
        }

        private void OnClickBtnCharacter(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.CharacterScreen);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            // Отключаем действия всех кнопок Home.
            _buttonStart?.UnregisterCallback<ClickEvent>(OnClickBtnStart);
            _buttonSelectLevel?.UnregisterCallback<ClickEvent>(OnClickBtnSelectLevel);
            _buttonLeaderboard?.UnregisterCallback<ClickEvent>(OnClickBtnLeaderboard);
            _buttonCharacter?.UnregisterCallback<ClickEvent>(OnClickBtnCharacter);
            _buttonQuests?.UnregisterCallback<ClickEvent>(OnClickBtnQuests);
            _buttonDevelopment?.UnregisterCallback<ClickEvent>(OnClickBtnDevelopment);
            _buttonShop?.UnregisterCallback<ClickEvent>(OnClickBtnShop);
        }

        protected override string ScreenBackgroundAddress => "HomeScreenSprite";

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return new ScreenLayout(content.Q<VisualElement>("homescreen"));
        }

        protected override void BindView()
        {
            RefreshExperiencePanel();
        }

        private void RefreshExperiencePanel()
        {
            var playerData = GameDataManager.PlayerData;
            if (playerData == null)
            {
                return;
            }

            var experienceThreshold = Math.Max(
                1,
                PlayerExperienceService.PlayerLevelThreshold);
            var experiencePoints = Math.Max(
                0,
                playerData.ExperiencePoints);

            // Заполняем runtime-текст и ограничиваем прогресс границами шкалы.
            var levelText = playerData.PlayerLevel.ToString();
            _playerLevel.text = levelText;
            _playerLevel.EnableInClassList(
                "home-xp-panel__level--compact",
                levelText.Length >= 3);
            _experienceLabel.text =
                $"{experiencePoints} / {experienceThreshold}";

            // Меняем только ширину fill внутри фиксированного clip-контейнера.
            var fillRatio = Mathf.Clamp01(
                (float)experiencePoints / experienceThreshold);
            _experienceFill.style.width = new StyleLength(
                Length.Percent(fillRatio * 100f));
            _experienceFill.style.display = fillRatio > 0f
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
