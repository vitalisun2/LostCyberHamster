using System;

namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Хранит локальное состояние виртуального устройства.</summary>
    public sealed class CloudSaveVirtualDeviceState
    {
        public CloudSaveVirtualDeviceState(
            string playerDataJson,
            string pendingSnapshotJson,
            string confirmedRevision)
        {
            if (string.IsNullOrWhiteSpace(playerDataJson))
                throw new ArgumentException("Player data must be provided.", nameof(playerDataJson));
            if (string.IsNullOrWhiteSpace(confirmedRevision))
                throw new ArgumentException("Confirmed revision must be provided.", nameof(confirmedRevision));

            PlayerDataJson = playerDataJson;
            PendingSnapshotJson = pendingSnapshotJson;
            ConfirmedRevision = confirmedRevision;
        }

        /// <summary>Прогресс игрока на устройстве.</summary>
        public string PlayerDataJson { get; }

        /// <summary>Снимок, ожидающий отправки.</summary>
        public string PendingSnapshotJson { get; }

        /// <summary>Последняя принятая облачная версия.</summary>
        public string ConfirmedRevision { get; }
    }
}
