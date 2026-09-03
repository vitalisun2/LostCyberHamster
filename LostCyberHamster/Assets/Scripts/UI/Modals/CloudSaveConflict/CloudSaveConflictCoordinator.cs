using System;
using System.Linq;
using System.Threading.Tasks;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Models;
using UnityEngine;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает неразрешённый Cloud Save конфликт только в главном меню.</summary>
    public sealed class CloudSaveConflictCoordinator
    {
        private readonly UIManager _uiManager;
        private readonly CloudSyncService _cloudSyncService;
        private readonly ConflictService _conflictService;
        private readonly CloudSaveConflictModalController _modalController;

        private bool _isEnabled;
        private bool _isVisible;
        private bool _isShowInProgress;
        private bool _isResolutionInProgress;
        private int _lifecycleVersion;

        public CloudSaveConflictCoordinator(
            UIManager uiManager,
            CloudSyncService cloudSyncService,
            ConflictService conflictService)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _cloudSyncService = cloudSyncService ?? throw new ArgumentNullException(nameof(cloudSyncService));
            _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));
            _modalController = uiManager.GetController<CloudSaveConflictModalController>();
        }

        /// <summary>Начинает показывать текущий и новые конфликты в меню.</summary>
        public void Enable()
        {
            if (_isEnabled)
                return;

            _isEnabled = true;
            _lifecycleVersion++;
            _conflictService.ConflictDetected += OnConflictDetected;
            _conflictService.ConflictResolved += OnConflictResolved;
            _modalController.CloudSelected += OnCloudSelected;
            _modalController.ThisDeviceSelected += OnThisDeviceSelected;
            PresentCurrentConflict();
        }

        /// <summary>Останавливает показ конфликтов при выходе из меню.</summary>
        public void Disable()
        {
            if (!_isEnabled)
                return;

            _isEnabled = false;
            _lifecycleVersion++;
            _conflictService.ConflictDetected -= OnConflictDetected;
            _conflictService.ConflictResolved -= OnConflictResolved;
            _modalController.CloudSelected -= OnCloudSelected;
            _modalController.ThisDeviceSelected -= OnThisDeviceSelected;
            _isVisible = false;
            _uiManager.CloseModal(ScreenEnum.CloudSaveConflictModal);
        }

        private void OnConflictDetected(CloudSaveConflict _)
        {
            if (!_isResolutionInProgress)
                PresentCurrentConflict();
        }

        private void OnConflictResolved()
        {
            _isVisible = false;
            _uiManager.CloseModal(ScreenEnum.CloudSaveConflictModal);
        }

        private void OnCloudSelected()
        {
            _ = ResolveAsync(_cloudSyncService.ResolveConflictWithCloudAsync);
        }

        private void OnThisDeviceSelected()
        {
            _ = ResolveAsync(_cloudSyncService.ResolveConflictWithLocalAsync);
        }

        private void PresentCurrentConflict()
        {
            var conflict = _conflictService.CurrentConflict;
            if (!_isEnabled || conflict == null)
                return;

            var data = CreateModalData(conflict);
            _modalController.SetData(data);

            if (!_isVisible && !_isShowInProgress)
                _ = ShowAsync();
        }

        private async Task ShowAsync()
        {
            var lifecycleVersion = _lifecycleVersion;
            _isShowInProgress = true;
            try
            {
                await _uiManager.ShowModalAsync(ScreenEnum.CloudSaveConflictModal);
                if (!IsLifecycleCurrent(lifecycleVersion) ||
                    _conflictService.CurrentConflict == null)
                {
                    _uiManager.CloseModal(ScreenEnum.CloudSaveConflictModal);
                    return;
                }

                _isVisible = true;
                _modalController.SetData(CreateModalData(_conflictService.CurrentConflict));
            }
            catch (Exception exception)
            {
                if (IsLifecycleCurrent(lifecycleVersion))
                {
                    Debug.LogError(
                        $"[CloudSave] Conflict modal show failed ({exception.GetType().Name}).");
                }
            }
            finally
            {
                _isShowInProgress = false;
                if (_isEnabled &&
                    !_isVisible &&
                    _conflictService.CurrentConflict != null)
                {
                    PresentCurrentConflict();
                }
            }
        }

        private async Task ResolveAsync(Func<Task<bool>> resolve)
        {
            if (!_isEnabled || !_isVisible || _isResolutionInProgress)
                return;

            var lifecycleVersion = _lifecycleVersion;
            _isResolutionInProgress = true;
            _modalController.SetBusy(isBusy: true);

            try
            {
                var resolved = await resolve();
                if (!IsLifecycleCurrent(lifecycleVersion))
                    return;

                if (resolved)
                {
                    _isVisible = false;
                    _uiManager.CloseModal(ScreenEnum.CloudSaveConflictModal);
                    UIManager.OnRepaintScreen?.Invoke();
                    return;
                }

                PresentCurrentConflict();
            }
            catch (Exception exception)
            {
                if (IsLifecycleCurrent(lifecycleVersion))
                {
                    Debug.LogError(
                        $"[CloudSave] Conflict resolution failed ({exception.GetType().Name}).");
                    PresentCurrentConflict();
                }
            }
            finally
            {
                _isResolutionInProgress = false;
                if (IsLifecycleCurrent(lifecycleVersion) && _isVisible)
                    _modalController.SetBusy(isBusy: false);
                else if (_isEnabled && _conflictService.CurrentConflict != null)
                    PresentCurrentConflict();
            }
        }

        private bool IsLifecycleCurrent(int lifecycleVersion)
        {
            return _isEnabled && lifecycleVersion == _lifecycleVersion;
        }

        private static CloudSaveConflictModalDto CreateModalData(CloudSaveConflict conflict)
        {
            return new CloudSaveConflictModalDto(
                CreateCard(conflict.CloudSave.Snapshot),
                CreateCard(conflict.LocalSnapshot));
        }

        private static CloudSaveConflictCardDto CreateCard(CloudSaveSnapshot snapshot)
        {
            var playerData = PlayerData.FromJson(snapshot.PlayerDataJson);
            var completedLevels = playerData.Progress.Entries.Count(entry => entry.IsCompleted);
            return new CloudSaveConflictCardDto(
                completedLevels,
                playerData.Money,
                playerData.Crystals,
                snapshot.SavedAtUtc);
        }
    }
}
