using Assets.Scripts.System;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Связывает кнопки Journey Complete с маршрутами meta-экранов.
    /// </summary>
    internal sealed class UiJourneyCompleteModalMechanics
    {
        private const string MenuSceneName = "Menu";

        private readonly LevelResultNavigationCoordinator _navigation;

        public UiJourneyCompleteModalMechanics(
            UIManager uiManager,
            LevelResultNavigationCoordinator navigation)
        {
            _navigation = navigation;

            var controller =
                uiManager.GetController<JourneyCompleteModalController>();
            controller.SetHomeAction(OnHome);
            controller.SetSkillsAction(OnSkills);
            controller.SetRankingsAction(OnRankings);
        }

        private void OnHome()
        {
            _navigation.Continue(
                ScreenEnum.JourneyCompleteModal,
                LoadMenu);
        }

        private void OnSkills()
        {
            _navigation.Continue(
                ScreenEnum.JourneyCompleteModal,
                () =>
                {
                    MenuNavigationRequest.OpenCharacterDevelopment();
                    LoadMenu();
                });
        }

        private void OnRankings()
        {
            if (!LevelManager.TryGetCurrentProgressKey(
                    out var progressKey))
            {
                return;
            }

            _navigation.Continue(
                ScreenEnum.JourneyCompleteModal,
                () =>
                {
                    MenuNavigationRequest.OpenLeaderboard(
                        progressKey.LocationId,
                        progressKey.PartOfDayId);
                    LoadMenu();
                });
        }

        private static void LoadMenu()
        {
            SceneManager.LoadScene(MenuSceneName);
        }
    }
}
