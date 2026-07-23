using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

namespace GameManagement.CloudSave.Gateway
{
    /// <summary>Хранит полный снимок в UGS Cloud Save.</summary>
    public sealed class UnityCloudSaveGateway : ICloudSaveGateway
    {
        /// <summary>Ключ полного снимка игрока.</summary>
        private const string SnapshotKey = "player_snapshot_";

        /// <summary>Возвращает облачный снимок или null.</summary>
        public async Task<CloudSaveReadResult> LoadSnapshotAsync()
        {
            // Ищем текущее сохранение.
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SnapshotKey });
            if (!loaded.TryGetValue(SnapshotKey, out var item))
                return null;

            if (item.Value == null || string.IsNullOrWhiteSpace(item.WriteLock))
                throw new InvalidOperationException("Cloud Save returned incomplete snapshot data.");

            // Собираем снимок с версией.
            var snapshot = CloudSaveSnapshot.FromJson(item.Value.GetAsString());
            var version = new CloudSaveVersion(item.WriteLock);
            return new CloudSaveReadResult(snapshot, version);
        }

        /// <summary>Сохраняет снимок поверх ожидаемой версии.</summary>
        public async Task<CloudSaveVersion> SaveSnapshotAsync(
            CloudSaveSnapshot snapshot,
            string expectedServerRevision)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (expectedServerRevision != null &&
                string.IsNullOrWhiteSpace(expectedServerRevision))
            {
                throw new ArgumentException(
                    "Expected server revision must not be empty.",
                    nameof(expectedServerRevision));
            }

            // Записываем снимок с защитой от чужих изменений.
            var data = new Dictionary<string, SaveItem>
            {
                [SnapshotKey] = new SaveItem(snapshot.ToJson(), expectedServerRevision)
            };

            var saved = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            if (!saved.TryGetValue(SnapshotKey, out var serverRevision) ||
                string.IsNullOrWhiteSpace(serverRevision))
                throw new InvalidOperationException("Cloud Save did not return snapshot revision.");

            return new CloudSaveVersion(serverRevision);
        }
    }
}
