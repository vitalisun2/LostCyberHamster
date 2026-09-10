using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    internal sealed class UiWinModalMechanics
    {
        private readonly LevelResultNavigationCoordinator _navigation;
        private readonly UIManager _uiManager;

        private const string SceneName = "Menu";

        public UiWinModalMechanics(
            UIManager uiManager,
            LevelResultNavigationCoordinator navigation)
        {
            _navigation = navigation;
            _uiManager = uiManager;

            var winModalController =
                uiManager.GetController<WinModalController>();

            winModalController.SetExitAction(OnExit);
            winModalController.SetRestartAction(OnRestart);
            winModalController.SetResumeAction(OnNextLevel);
            winModalController.SetLeaderboardAction(OnLeaderboard);
            winModalController.SetGoalAction(OnGoal);
        }

        private void OnExit()
        {
            _navigation.Continue(
                ScreenEnum.WinModal,
                () => SceneManager.LoadScene(SceneName));
        }

        private void OnRestart()
        {
            _navigation.Continue(
                ScreenEnum.WinModal,
                () => LevelController.Instance.Replay(), GameDataManager.PlayerData.CurrentLevel);
        }

        private void OnNextLevel()
        {
            if (LevelManager.IsLastAvailableLevel(GameDataManager.PlayerData.CurrentLevel))
            {
                _uiManager.CloseModal(ScreenEnum.WinModal);
                _uiManager.ShowModalAsync(ScreenEnum.JourneyCompleteModal);
                return;
            }
            LevelManager.TryGetNextLevelKey(GameDataManager.PlayerData.CurrentLevel, out string nextLevel);
            _navigation.Continue(
                ScreenEnum.WinModal,
                () => LevelController.Instance.PlayNextLevel(), nextLevel);
        }

        private void OnLeaderboard(string locationId, string partId)
        {
            _navigation.Continue(ScreenEnum.WinModal, () =>
            {
                MenuNavigationRequest.OpenLeaderboard(locationId, partId);
                SceneManager.LoadScene(SceneName);
            }, returnScreen: ScreenEnum.LeaderboardScreen, location: locationId, part: partId);
        }

        private void OnGoal(NextGoalCandidate goal)
        {
            if (goal?.IsCurrentProfile != true) return;

            // Урок щита сохраняет следующий уровень для возвращения из обучения.
            if (goal.StartsShieldLesson)
            {
                LevelManager.TryGetNextLevelKey(GameDataManager.PlayerData.CurrentLevel, out string nextLevel);
                _navigation.Continue(ScreenEnum.WinModal,
                    () => FirstSessionNavigation.Begin(_uiManager, nextLevel), nextLevel, startShieldLesson: true);
                return;
            }

            // PrepareResume после Level Up может заменить callback; target готовим заранее.
            NextGoalNavigation.Prepare(goal);
            _navigation.Continue(ScreenEnum.WinModal,
                () => NextGoalNavigation.Open(_uiManager, goal), returnScreen: goal.Destination);
        }
    }
}
