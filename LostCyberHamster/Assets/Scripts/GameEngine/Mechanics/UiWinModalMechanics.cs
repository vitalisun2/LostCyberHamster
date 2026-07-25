using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using LostCyberHamster.UI;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiWinModalMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private WinModalController _winModalController;

        private string _sceneName = "Menu";

        public UiWinModalMechanics(UIManager uiManager, GameManager gameManager)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;

            _winModalController = _uiManager.GetController<WinModalController>();

            _winModalController.SetExitAction(OnExit);
            _winModalController.SetRestartAction(OnRestart);
            _winModalController.SetResumeAction(OnNextLevel);
            _winModalController.SetLeaderboardAction(OnLeaderboard);
        }

        private void OnExit()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
        }

        private void OnRestart()
        {
            _winModalController.Close();
            LevelController.Instance.Replay();
        }

        private void OnNextLevel()
        {
            _winModalController.Close();
            LevelController.Instance.PlayNextLevel();
        }

        private void OnLeaderboard(string locationId, string partId)
        {
            MenuNavigationRequest.OpenLeaderboard(locationId, partId);
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
        }
    }
}
