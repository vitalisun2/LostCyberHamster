using System;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;

namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Переключает локальное состояние между двумя виртуальными устройствами.</summary>
    public sealed class CloudSaveVirtualDeviceStorage
    {
        /// <summary>Первое виртуальное устройство.</summary>
        public const string DeviceA = "A";

        /// <summary>Второе виртуальное устройство.</summary>
        public const string DeviceB = "B";

        /// <summary>Управляет ожидающим снимком.</summary>
        private readonly SnapshotService _snapshotService;

        /// <summary>Хранит подтверждённую облачную версию.</summary>
        private readonly ICloudSaveVersionStore _versionStore;

        /// <summary>Локальное состояние первого устройства.</summary>
        private CloudSaveVirtualDeviceState _deviceA;

        /// <summary>Локальное состояние второго устройства.</summary>
        private CloudSaveVirtualDeviceState _deviceB;

        /// <summary>Имя активного устройства.</summary>
        private string _activeDeviceName;

        /// <summary>Игрок обоих устройств.</summary>
        private string _playerId;

        public CloudSaveVirtualDeviceStorage(
            SnapshotService snapshotService,
            ICloudSaveVersionStore versionStore)
        {
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
        }

        /// <summary>Создаёт два одинаковых устройства из текущего локального состояния.</summary>
        public void Initialize(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));

            _playerId = playerId;
            var current = CaptureCurrent();

            // Оба устройства начинают с одной подтверждённой версии прогресса.
            _deviceA = Clone(current);
            _deviceB = Clone(current);
            _activeDeviceName = DeviceA;
        }

        /// <summary>Сохраняет активное устройство и переключается на выбранное.</summary>
        public void SwitchTo(string deviceName)
        {
            ValidateDeviceName(deviceName);
            EnsureInitialized();

            // Не теряем локальные изменения активного устройства.
            CaptureActive();

            // Восстанавливаем состояние выбранного устройства.
            Apply(GetState(deviceName));
            _activeDeviceName = deviceName;
        }

        /// <summary>Запоминает текущее локальное состояние активного устройства.</summary>
        public void CaptureActive()
        {
            EnsureInitialized();

            var state = CaptureCurrent();
            if (string.Equals(_activeDeviceName, DeviceA, StringComparison.Ordinal))
                _deviceA = state;
            else
                _deviceB = state;
        }

        /// <summary>Снимает копию текущего локального состояния.</summary>
        private CloudSaveVirtualDeviceState CaptureCurrent()
        {
            var confirmedRevision = _versionStore.GetConfirmedRevision(_playerId);
            if (string.IsNullOrWhiteSpace(confirmedRevision))
                throw new InvalidOperationException("Confirmed cloud revision is required.");

            return new CloudSaveVirtualDeviceState(
                GameDataManager.PlayerData.ToJson(),
                _snapshotService.GetPending(_playerId)?.ToJson(),
                confirmedRevision);
        }

        /// <summary>Применяет локальное состояние устройства.</summary>
        private void Apply(CloudSaveVirtualDeviceState state)
        {
            // Восстанавливаем прогресс и ожидающую отправку.
            GameDataManager.ReplacePlayerData(PlayerData.FromJson(state.PlayerDataJson));
            if (state.PendingSnapshotJson == null)
                _snapshotService.Clear(_playerId);
            else
                _snapshotService.SetPending(CloudSaveSnapshot.FromJson(state.PendingSnapshotJson));

            // Возвращаем подтверждённую версию устройства.
            _versionStore.SaveConfirmedVersion(_playerId, state.ConfirmedRevision);
        }

        /// <summary>Возвращает состояние выбранного устройства.</summary>
        private CloudSaveVirtualDeviceState GetState(string deviceName)
        {
            return string.Equals(deviceName, DeviceA, StringComparison.Ordinal)
                ? _deviceA
                : _deviceB;
        }

        /// <summary>Проверяет готовность виртуальных устройств.</summary>
        private void EnsureInitialized()
        {
            if (_deviceA == null || _deviceB == null || string.IsNullOrWhiteSpace(_activeDeviceName))
                throw new InvalidOperationException("Virtual devices are not initialized.");
        }

        /// <summary>Проверяет имя виртуального устройства.</summary>
        private static void ValidateDeviceName(string deviceName)
        {
            if (!string.Equals(deviceName, DeviceA, StringComparison.Ordinal) &&
                !string.Equals(deviceName, DeviceB, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unknown virtual device.", nameof(deviceName));
            }
        }

        /// <summary>Создаёт независимое состояние устройства.</summary>
        private static CloudSaveVirtualDeviceState Clone(CloudSaveVirtualDeviceState state)
        {
            return new CloudSaveVirtualDeviceState(
                state.PlayerDataJson,
                state.PendingSnapshotJson,
                state.ConfirmedRevision);
        }
    }
}
