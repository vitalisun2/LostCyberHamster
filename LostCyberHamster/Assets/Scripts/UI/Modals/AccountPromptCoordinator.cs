using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameManagement;
using UnityEngine;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает одноразовое предложение привязать гостевой аккаунт при подходящем состоянии меню.
    /// </summary>
    public sealed class AccountPromptCoordinator
    {
        private readonly UIManager _uiManager;
        private readonly AccountService _accountService;

        private bool _isEnabled;
        private bool _isShowInProgress;

        public AccountPromptCoordinator(UIManager uiManager, AccountService accountService)
        {
            _uiManager = uiManager;
            _accountService = accountService;
        }

        public void Enable()
        {
            if (_isEnabled)
                return;

            _isEnabled = true;
            _accountService.StateChanged -= OnAccountStateChanged;
            _accountService.StateChanged += OnAccountStateChanged;
            TryShow();
        }

        public void Disable()
        {
            _isEnabled = false;
            _accountService.StateChanged -= OnAccountStateChanged;
        }

        private void OnAccountStateChanged(AccountState state)
        {
            if (state != AccountState.Resolving)
                TryShow();
        }

        private void TryShow()
        {
            if (_isShowInProgress || !CanShow())
                return;

            _ = ShowAsync();
        }

        private bool CanShow()
        {
            var playerData = GameDataManager.PlayerData;
            return _isEnabled &&
                   playerData != null &&
                   playerData.IsAccountPromptPending &&
                   !playerData.IsAccountPromptShown &&
                   _accountService.State == AccountState.Guest &&
                   Application.internetReachability != NetworkReachability.NotReachable;
        }

        private async Task ShowAsync()
        {
            _isShowInProgress = true;

            try
            {
                // Сначала дожидаемся фактического отображения модального окна.
                await _uiManager.ShowModalAsync(ScreenEnum.AccountPromptModal);

                // Не оставляем окно открытым, если за время загрузки изменился lifecycle или аккаунт.
                if (!CanShow())
                {
                    ClosePrompt();
                    return;
                }

                // Фиксируем одноразовый показ только после успешного отображения.
                var playerData = GameDataManager.PlayerData;
                playerData.IsAccountPromptPending = false;
                playerData.IsAccountPromptShown = true;
                PlayerProgressCommitter.Commit(CheckpointReason.AccountPromptStateChanged);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Account] Prompt show failed: {exception.GetType().Name}.");
            }
            finally
            {
                _isShowInProgress = false;
            }
        }

        private void ClosePrompt()
        {
            var controller = _uiManager.GetController<AccountPromptModalController>();
            controller.UnsubscribeFromEvents();
            controller.Close();
        }
    }
}
