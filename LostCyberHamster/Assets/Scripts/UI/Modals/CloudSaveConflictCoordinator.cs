using System;
using System.Linq;
using System.Threading.Tasks;
using GameManagement;
using GameManagement.CloudSave;
using UnityEngine;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает неразрешённый Cloud Save конфликт только в главном меню.</summary>
    public sealed class CloudSaveConflictCoordinator
    {
        private readonly UIManager _uiManager;
        private readonly CloudSyncService _cloudSyncService;
        private readonly CloudSaveConflictModalController _modalController;

        private bool _isEnabled;
        private bool _isVisible;
        private bool _isShowInProgress;
        private bool _isResolutionInProgress;

        public CloudSaveConflictCoordinator(UIManager uiManager, CloudSyncService cloudSyncService)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _cloudSyncService = cloudSyncService ?? throw new ArgumentNullException(nameof(cloudSyncService));
            _modalController = uiManager.GetController<CloudSaveConflictModalController>();
        }

        /// <summary>Начинает показывать текущий и новые конфликты в меню.</summary>
        public void Enable()
        {
            if (_isEnabled)
                return;

            _isEnabled = true;
            _cloudSyncService.ConflictDetected += OnConflictDetected;
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
            _cloudSyncService.ConflictDetected -= OnConflictDetected;
            _modalController.CloudSelected -= OnCloudSelected;
            _modalController.ThisDeviceSelected -= OnThisDeviceSelected;
        }

        private void OnConflictDetected(CloudSaveConflict _)
        {
            if (!_isResolutionInProgress)
                PresentCurrentConflict();
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
            var conflict = _cloudSyncService.CurrentConflict;
            if (!_isEnabled || conflict == null)
                return;

            var data = CreateModalData(conflict);
            _modalController.SetData(data);

            if (!_isVisible && !_isShowInProgress)
                _ = ShowAsync();
        }

        private async Task ShowAsync()
        {
            _isShowInProgress = true;
            try
            {
                await _uiManager.ShowModalAsync(ScreenEnum.CloudSaveConflictModal);
                if (!_isEnabled || _cloudSyncService.CurrentConflict == null)
                {
                    _modalController.Close();
                    return;
                }

                _isVisible = true;
                _modalController.SetData(CreateModalData(_cloudSyncService.CurrentConflict));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Conflict modal show failed ({exception.GetType().Name}).");
            }
            finally
            {
                _isShowInProgress = false;
            }
        }

        private async Task ResolveAsync(Func<Task<bool>> resolve)
        {
            if (!_isEnabled || !_isVisible || _isResolutionInProgress)
                return;

            _isResolutionInProgress = true;
            _modalController.SetBusy(isBusy: true);

            try
            {
                if (await resolve())
                {
                    _isVisible = false;
                    _modalController.Close();
                    UIManager.OnRepaintScreen?.Invoke();
                    return;
                }

                PresentCurrentConflict();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Conflict resolution failed ({exception.GetType().Name}).");
                PresentCurrentConflict();
            }
            finally
            {
                _isResolutionInProgress = false;
                if (_isVisible)
                    _modalController.SetBusy(isBusy: false);
            }
        }

        private static CloudSaveConflictModalData CreateModalData(CloudSaveConflict conflict)
        {
            return new CloudSaveConflictModalData(
                CreateCard(conflict.CloudSnapshot, conflict.CloudVersion.ServerModifiedAtUtc),
                CreateCard(conflict.LocalSnapshot, ParseSavedAt(conflict.LocalSnapshot.SavedAtUtc)));
        }

        private static CloudSaveConflictCardData CreateCard(CloudSaveSnapshot snapshot, DateTime savedAt)
        {
            var playerData = CloudSaveSnapshotCodec.RestorePlayerData(snapshot);
            var completedLevels = playerData.Progress.Entries.Count(entry => entry.IsCompleted);
            return new CloudSaveConflictCardData(
                completedLevels,
                playerData.Money,
                playerData.Crystals,
                savedAt);
        }

        private static DateTime ParseSavedAt(string value)
        {
            return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var savedAt)
                ? savedAt
                : DateTime.MinValue;
        }
    }
}
