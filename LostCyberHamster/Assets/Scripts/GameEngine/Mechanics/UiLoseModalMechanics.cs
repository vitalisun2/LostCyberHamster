using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameAds;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>Воскрешает только исходную живую попытку после сохранённого результата рекламы.</summary>
    public class UiLoseModalMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _character;
        private readonly LoseModalController _loseModalController;
        private readonly string _runId = Guid.NewGuid().ToString("N");
        private readonly int _sceneHandle;
        private RewardedAdRequest _request;
        private bool _runEnded;

        public UiLoseModalMechanics(UIManager uiManager, GameManager gameManager, Hamster character)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _character = character;
            _sceneHandle = character.gameObject.scene.handle;
            _loseModalController = _uiManager.GetController<LoseModalController>();
            _loseModalController.SetExitAction(OnExit);
            _loseModalController.SetRestartAction(OnRestart);
            _loseModalController.SetWatchAdsAction(OnWatchAd);
        }

        private void OnExit()
        {
            EndAttempt();
            SceneManager.LoadScene("Menu");
        }

        private void OnRestart()
        {
            EndAttempt();
            _uiManager.CloseModal(ScreenEnum.LoseModal);
            LevelController.Instance.Replay();
        }

        private void EndAttempt()
        {
            _runEnded = true;
            RewardedAdService.Instance.CancelContext(_request);
            _request = null;
        }

        private void OnWatchAd()
        {
            if (_runEnded || (_request != null && !_request.IsFinished))
                return;
            _request = RewardedAdService.Instance.RequestRevive(_runId, _sceneHandle,
                () => !_runEnded && _character != null && _gameManager != null &&
                    _character.gameObject.scene.handle == _sceneHandle && _character.Lives.Value <= 0,
                Revive);
            _loseModalController.SetAdvertisementRequest(_request);
        }

        private void Revive()
        {
            _character.Lives.Value = 1;
            _uiManager.CloseModal(ScreenEnum.LoseModal);
            _gameManager.Resume();
        }
    }
}
