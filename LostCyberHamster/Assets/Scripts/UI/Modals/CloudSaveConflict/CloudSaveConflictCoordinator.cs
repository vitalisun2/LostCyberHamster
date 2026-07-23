using System;
using System.Linq;
using System.Threading.Tasks;
using GameManagement;
using GameManagement.CloudSave_;
using GameManagement.CloudSave_.Models;
using UnityEngine;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает неразрешённый Cloud Save конфликт только в главном меню.</summary>
    public sealed class CloudSaveConflictCoordinator
    {
        private readonly UIManager _uiManager;
        private readonly ConflictService_ _conflictService;
        private readonly CloudSaveConflictModalController _modalController;

        private bool _isEnabled;
        private bool _isVisible;
        private bool _isShowInProgress;
        private bool _isResolutionInProgress;

        public CloudSaveConflictCoordinator(
            UIManager uiManager,
            ConflictService_ conflictService)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));
            _modalController = uiManager.GetController<CloudSaveConflictModalController>();
        }

        /// <summary>Начинает показывать текущий и новые конфликты в меню.</summary>
        public void Enable()
        {
            if (_isEnabled)
                return;

            _isEnabled = true;
            _conflictService.ConflictDetected += OnConflictDetected;
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
            _conflictService.ConflictDetected -= OnConflictDetected;
            _modalController.CloudSelected -= OnCloudSelected;
            _modalController.ThisDeviceSelected -= OnThisDeviceSelected;
        }

        private void OnConflictDetected(CloudSaveConflict_ _)
        {
            if (!_isResolutionInProgress)
                PresentCurrentConflict();
        }

        private void OnCloudSelected()
        {
            _ = ResolveAsync(_conflictService.ResolveWithCloudAsync);
        }

        private void OnThisDeviceSelected()
        {
            _ = ResolveAsync(_conflictService.ResolveWithLocalAsync);
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
            _isShowInProgress = true;
            try
            {
                await _uiManager.ShowModalAsync(ScreenEnum.CloudSaveConflictModal);
                if (!_isEnabled || _conflictService.CurrentConflict == null)
                {
                    _modalController.Close();
                    return;
                }

                _isVisible = true;
                _modalController.SetData(CreateModalData(_conflictService.CurrentConflict));
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

        private async Task ResolveAsync(Func<Task> resolve)
        {
            if (!_isEnabled || !_isVisible || _isResolutionInProgress)
                return;

            _isResolutionInProgress = true;
            _modalController.SetBusy(isBusy: true);

            try
            {
                await resolve();
                _isVisible = false;
                _modalController.Close();
                UIManager.OnRepaintScreen?.Invoke();
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

        private static CloudSaveConflictModalDto CreateModalData(CloudSaveConflict_ conflict)
        {
            return new CloudSaveConflictModalDto(
                CreateCard(conflict.CloudSave.Snapshot),
                CreateCard(conflict.LocalSnapshot));
        }

        private static CloudSaveConflictCardDto CreateCard(CloudSaveSnapshot_ snapshot)
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
