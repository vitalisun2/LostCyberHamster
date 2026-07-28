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
        private readonly XpRewardBannerController
            _xpRewardBannerController;
        private WinModalController _winModalController;

        private string _sceneName = "Menu";
        private int _previousPlayerLevel;
        private bool _isTransitionPending;

        public UiWinModalMechanics(
            UIManager uiManager,
            GameManager gameManager,
            XpRewardBannerController xpRewardBannerController)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _xpRewardBannerController =
                xpRewardBannerController;
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

        private async void OnLeaderboard(
            string locationId,
            string partId)
        {
            if (_isTransitionPending)
            {
                return;
            }

            _isTransitionPending = true;
            try
            {
                // Сохраняем существующий маршрут после полного показа XP.
                await _xpRewardBannerController
                    .WaitForCompletionAsync();
                MenuNavigationRequest.OpenLeaderboard(
                    locationId,
                    partId);
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    _sceneName);
            }
            catch (Exception exception)
            {
                _isTransitionPending = false;
                Debug.LogException(exception);
            }
        }

        private async void ContinueAfterLevelUp(Action action)
        {
            if (_isTransitionPending)
            {
                return;
            }

            _isTransitionPending = true;
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
                // Ждём полного показа награды перед переходом из WinModal.
                await _xpRewardBannerController
                    .WaitForCompletionAsync();

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
