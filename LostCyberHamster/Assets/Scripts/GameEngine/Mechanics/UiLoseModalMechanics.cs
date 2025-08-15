using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiLoseModalMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _character;
        private LoseModalController _loseModalController;
        private string _sceneName = "Menu";

        public UiLoseModalMechanics(UIManager uiManager, GameManager gameManager, Hamster character)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _character = character;

            _loseModalController = _uiManager.GetController<LoseModalController>();

            _loseModalController.SetExitAction(OnExit);
            _loseModalController.SetRestartAction(OnRestart);
            _loseModalController.SetWatchAdsAction(OnWatchAd);
        }

        private void OnExit()
        {
            SceneManager.LoadScene(_sceneName);
        }

        private void OnRestart()
        {
            _loseModalController.Close();
            LevelController.Instance.Replay();
        }

        private void OnWatchAd()
        {
            GameEventsManager.OnAdCompleted += HandleAdCompleted;
            GameEventsManager.ShowAd();
        }

        private void HandleAdCompleted()
        {
            GameEventsManager.OnAdCompleted -= HandleAdCompleted;
            _character.Lives.Value = 1;
            _loseModalController.Close();
            _gameManager.Resume();
        }
    }
}
