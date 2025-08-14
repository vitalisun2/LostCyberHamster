using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiPauseScreenMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private string _sceneName = "Menu";
        private PauseModalController _pauseModalController;

        public UiPauseScreenMechanics(UIManager uiManager, GameManager gameManager)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;

            _pauseModalController = _uiManager.GetController<PauseModalController>();

            _pauseModalController.SetResumeAction(OnResume);
            _pauseModalController.SetExitAction(OnExit);
            _pauseModalController.SetRestartAction(OnRestart);
        }

        private void OnResume()
        {
            _pauseModalController.Close();
            _gameManager.Resume();
        }

        private void OnExit()
        {
            SceneManager.LoadScene(_sceneName);
        }

        private void OnRestart()
        {
            _pauseModalController.Close();
            LevelController.Instance.Replay();
        }
    }
}
