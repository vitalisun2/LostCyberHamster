using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameManagement;
using GameManagement.CloudSave;
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
        private readonly CloudSyncService _cloudSyncService;

        private bool _isEnabled;
        private bool _isShowInProgress;
        private int _lifecycleVersion;
        private double _retryAt;

        public AccountPromptCoordinator(UIManager uiManager, AccountService accountService,
            CloudSyncService cloudSyncService = null)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _cloudSyncService = cloudSyncService;
        }

        public void Enable()
        {
            if (_isEnabled)
                return;

            _isEnabled = true;
            _lifecycleVersion++;
            _accountService.StateChanged -= OnAccountStateChanged;
            _accountService.StateChanged += OnAccountStateChanged;
            TryShow();
        }

        public void Disable()
        {
            _isEnabled = false;
            _lifecycleVersion++;
            _accountService.StateChanged -= OnAccountStateChanged;
            ClosePrompt();
        }

        /// <summary>Повторяет сохранённый pending после освобождения меню.</summary>
        public void Tick() => TryShow();

        private void OnAccountStateChanged(AccountState state)
        {
            if (state != AccountState.Resolving)
                TryShow();
        }

        private void TryShow()
        {
            if (_isShowInProgress || Time.realtimeSinceStartupAsDouble < _retryAt ||
                _uiManager.HasModalOrTransition || _uiManager.HasPriorityPresentation || !CanShow())
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
                   !HasPriorityConflict() &&
                   _accountService.State == AccountState.Guest &&
                   Application.internetReachability != NetworkReachability.NotReachable;
        }

        private async Task ShowAsync()
        {
            _isShowInProgress = true;
            int lifecycleVersion = _lifecycleVersion;
            string profile = GameDataManager.ProfileId;
            long generation = GameDataManager.Generation;

            try
            {
                // Сначала дожидаемся фактического отображения модального окна.
                await _uiManager.ShowModalAsync(ScreenEnum.AccountPromptModal);
                if (_uiManager.CurrentModal != ScreenEnum.AccountPromptModal) return;

                // Не оставляем окно открытым, если за время загрузки изменился lifecycle или аккаунт.
                if (!CanShow() || lifecycleVersion != _lifecycleVersion ||
                    GameDataManager.ProfileId != profile || GameDataManager.Generation != generation)
                {
                    ClosePrompt();
                    return;
                }

                // Фиксируем одноразовый показ только после успешного отображения.
                GameDataManager.ExecuteTransaction(CheckpointReason.AccountPromptStateChanged, () =>
                {
                    var playerData = GameDataManager.PlayerData;
                    playerData.IsAccountPromptPending = false;
                    playerData.IsAccountPromptShown = true;
                });
            }
            catch (Exception exception)
            {
                _retryAt = Time.realtimeSinceStartupAsDouble + 5;
                ClosePrompt();
                Debug.LogWarning($"[Account] Prompt show failed: {exception.GetType().Name}.");
            }
            finally
            {
                _isShowInProgress = false;
            }
        }

        private bool HasPriorityConflict() => _cloudSyncService != null &&
            _cloudSyncService.HasUnresolvedConflict && !_cloudSyncService.IsConflictDeferred;

        private void ClosePrompt()
        {
            _uiManager.CloseModal(ScreenEnum.AccountPromptModal);
        }
    }
}
