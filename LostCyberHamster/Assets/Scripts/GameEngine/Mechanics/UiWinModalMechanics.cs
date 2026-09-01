using Assets.Scripts.System;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    internal sealed class UiWinModalMechanics
    {
        private readonly LevelResultNavigationCoordinator _navigation;

        private const string SceneName = "Menu";

        public UiWinModalMechanics(
            UIManager uiManager,
            LevelResultNavigationCoordinator navigation)
        {
            _navigation = navigation;

            var winModalController =
                uiManager.GetController<WinModalController>();

            winModalController.SetExitAction(OnExit);
            winModalController.SetRestartAction(OnRestart);
            winModalController.SetResumeAction(OnNextLevel);
            winModalController.SetLeaderboardAction(OnLeaderboard);
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
                () => LevelController.Instance.Replay());
        }

        private void OnNextLevel()
        {
            _navigation.Continue(
                ScreenEnum.WinModal,
                () => LevelController.Instance.PlayNextLevel());
        }

        private void OnLeaderboard(string locationId, string partId)
        {
            MenuNavigationRequest.OpenLeaderboard(locationId, partId);
            SceneManager.LoadScene(SceneName);
        }
    }
}
