using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiWinModalMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private WinModalController _winModalController;

        private string _sceneName = "Menu";
        private int _previousPlayerLevel;

        public UiWinModalMechanics(UIManager uiManager, GameManager gameManager)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _previousPlayerLevel =
                GameDataManager.PlayerData.PlayerLevel;

            _winModalController = _uiManager.GetController<WinModalController>();

            _winModalController.SetExitAction(OnExit);
            _winModalController.SetRestartAction(OnRestart);
            _winModalController.SetResumeAction(OnNextLevel);
            _winModalController.SetLeaderboardAction(OnLeaderboard);
        }

        private void OnExit()
        {
            ContinueAfterLevelUp(
                () => UnityEngine.SceneManagement.SceneManager.LoadScene(
                    _sceneName));
        }

        private void OnRestart()
        {
            ContinueAfterLevelUp(
                () => LevelController.Instance.Replay());
        }

        private void OnNextLevel()
        {
            ContinueAfterLevelUp(
                () => LevelController.Instance.PlayNextLevel());
        }

        private void OnLeaderboard(string locationId, string partId)
        {
            MenuNavigationRequest.OpenLeaderboard(locationId, partId);
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
        }

        private async void ContinueAfterLevelUp(Action action)
        {
            bool actionInvoked = false;

            void InvokeActionOnce()
            {
                if (actionInvoked)
                {
                    return;
                }

                actionInvoked = true;
                action.Invoke();
            }

            try
            {
                int previousPlayerLevel = _previousPlayerLevel;
                int currentPlayerLevel =
                    GameDataManager.PlayerData.PlayerLevel;
                SuperAttackService.TryGetFirstUnlockedBetweenLevels(
                    previousPlayerLevel,
                    currentPlayerLevel,
                    out SuperAttackData unlockedSuperAttack);

                // Фиксируем обработанный уровень до асинхронного показа следующей модалки.
                _previousPlayerLevel = currentPlayerLevel;
                _uiManager.CloseModal(ScreenEnum.WinModal);

                if (unlockedSuperAttack == null)
                {
                    InvokeActionOnce();
                    return;
                }

                // Показываем открытие суперудара перед исходным действием пользователя.
                var levelUpModalController =
                    _uiManager.GetController<LevelUpModalController>();
                levelUpModalController.SetLevelUpData(
                    previousPlayerLevel,
                    currentPlayerLevel,
                    unlockedSuperAttack);
                levelUpModalController.SetOkAction(
                    InvokeActionOnce);
                await _uiManager.ShowModalAsync(
                    ScreenEnum.LevelUpModal);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                InvokeActionOnce();
            }
        }
    }
}
