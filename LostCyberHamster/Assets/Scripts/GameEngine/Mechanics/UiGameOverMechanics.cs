using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Atomic.Elements;
using GameManagement;
using LostCyberHamster.UI;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiGameOverMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _hamster;

        public UiGameOverMechanics(UIManager uiManager, GameManager gameManager, Hamster hamster)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _hamster = hamster;
        }

        public void Subscribe()
        {
            _gameManager.OnFinish += OnFinish;
        }

        public void Unsubscribe()
        {
            _gameManager.OnFinish -= OnFinish;
        }

        private void OnFinish()
        {
            if(_hamster.Lives.Value == 0)
            {
                _uiManager.ShowModalAsync(ScreenEnum.LoseModal);
            }
            else
            {
                var completedLevelNumber = LevelManager.GetCurrentLevelNumber();
                GameEventsManager.LevelCompleted(completedLevelNumber, _hamster.Lives.Value);

                var playerData = GameDataManager.PlayerData;
                if (completedLevelNumber == 1 &&
                    playerData != null &&
                    !playerData.IsAccountPromptPending &&
                    !playerData.IsAccountPromptShown)
                {
                    playerData.IsAccountPromptPending = true;
                    GameDataManager.SaveData();
                }

                var winScreenController = _uiManager.GetController<WinModalController>();
                winScreenController.SetParamsForInit(LevelManager.GetLocationName(), LevelManager.GetCurrentPartOfDay(), _hamster.Lives.Value);
                _uiManager.ShowModalAsync(ScreenEnum.WinModal);
            }
        }
    }
}
