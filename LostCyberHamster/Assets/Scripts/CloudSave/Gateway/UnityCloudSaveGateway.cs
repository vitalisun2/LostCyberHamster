using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Адаптирует UGS Cloud Save 3.2.0 к одному полному снимку прогресса.
    /// </summary>
    public sealed class UnityCloudSaveGateway : ICloudSaveGateway
    {
        /// <summary>Ключ полного снимка в UGS Cloud Save.</summary>
        private const string SnapshotKey = "player_snapshot";

        /// <summary>Загружает полный снимок и его серверные метаданные.</summary>
        public async Task<CloudSaveReadResult> LoadSnapshotAsync()
        {
            // Загружаем запись полного снимка.
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SnapshotKey });

            // Отсутствующая запись означает, что снимок ещё не создан.
            if (!loaded.TryGetValue(SnapshotKey, out var item))
            {
                return null;
            }

            // Проверяем обязательные серверные метаданные.
            if (item.Value == null ||
                string.IsNullOrWhiteSpace(item.WriteLock) ||
                !item.Modified.HasValue)
            {
                throw new InvalidOperationException("Cloud Save returned incomplete snapshot metadata.");
            }

            // Восстанавливаем снимок и результат чтения.
            var snapshot = CloudSaveSnapshotCodec.Deserialize(item.Value.GetAsString());
            return new CloudSaveReadResult(snapshot, item.WriteLock, item.Modified.Value);
        }

        /// <summary>Условно записывает полный снимок и возвращает подтверждённые метаданные.</summary>
        public async Task<CloudSaveWriteResult> SaveSnapshotAsync(CloudSaveSnapshotDto snapshot)
        {
            // Проверяем входной снимок.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // Готовим запись с revision исходной cloud version.
            var writeLock = string.IsNullOrWhiteSpace(snapshot.BaseRevision)
                ? null
                : snapshot.BaseRevision;
            var data = new Dictionary<string, SaveItem>
            {
                [SnapshotKey] = new SaveItem(CloudSaveSnapshotCodec.Serialize(snapshot), writeLock)
            };

            // Записываем снимок и проверяем полученную revision.
            var saved = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            if (!saved.TryGetValue(SnapshotKey, out var serverRevision) ||
                string.IsNullOrWhiteSpace(serverRevision))
            {
                throw new InvalidOperationException("Cloud Save did not return snapshot revision.");
            }

            // Перечитываем запись для подтверждения итоговых метаданных.
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SnapshotKey });
            if (!loaded.TryGetValue(SnapshotKey, out var item) ||
                !item.Modified.HasValue ||
                !string.Equals(item.WriteLock, serverRevision, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cloud Save did not confirm saved snapshot metadata.");
            }

            // Возвращаем подтверждённую сервером версию.
            return new CloudSaveWriteResult(serverRevision, item.Modified.Value);
        }
    }
}
