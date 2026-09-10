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
        private readonly UIManager _uiManager;

        public UiJourneyCompleteModalMechanics(
            UIManager uiManager,
            LevelResultNavigationCoordinator navigation)
        {
            _navigation = navigation;
            _uiManager = uiManager;

            var controller =
                uiManager.GetController<JourneyCompleteModalController>();
            controller.SetHomeAction(OnHome);
            controller.SetSkillsAction(OnSkills);
            controller.SetRankingsAction(OnRankings);
            controller.SetGoalAction(OnGoal);
        }

        private void OnGoal(NextGoalCandidate goal)
        {
            if (goal?.IsCurrentProfile != true) return;
            // LevelUp может заменить callback своим PrepareResume; target готовим до очереди.
            NextGoalNavigation.Prepare(goal);
            _navigation.Continue(ScreenEnum.JourneyCompleteModal,
                () => NextGoalNavigation.Open(_uiManager, goal), returnScreen: goal.Destination);
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
                }, returnScreen: ScreenEnum.CharacterDevelopmentScreen);
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
                }, returnScreen: ScreenEnum.LeaderboardScreen,
                location: progressKey.LocationId, part: progressKey.PartOfDayId);
        }

        private static void LoadMenu()
        {
            SceneManager.LoadScene(MenuSceneName);
        }
    }
}
