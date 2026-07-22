using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameManagement.CloudSave_.Models;
using GameManagement.CloudSave_.Version;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

namespace GameManagement.CloudSave_.Gateway
{
    /// <summary>Хранит полный снимок в UGS Cloud Save.</summary>
    public sealed class UnityCloudSaveGateway_ : ICloudSaveGateway_
    {
        /// <summary>Ключ полного снимка игрока.</summary>
        private const string SnapshotKey = "player_snapshot_";

        /// <summary>Возвращает облачный снимок или null.</summary>
        public async Task<CloudSaveReadResult_> LoadSnapshotAsync()
        {
            // Ищем текущее сохранение.
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SnapshotKey });
            if (!loaded.TryGetValue(SnapshotKey, out var item))
                return null;

            if (item.Value == null || string.IsNullOrWhiteSpace(item.WriteLock))
                throw new InvalidOperationException("Cloud Save returned incomplete snapshot data.");

            // Собираем снимок с версией.
            var snapshot = CloudSaveSnapshot_.FromJson(item.Value.GetAsString());
            var version = new CloudSaveVersion_(item.WriteLock);
            return new CloudSaveReadResult_(snapshot, version);
        }

        /// <summary>Сохраняет снимок поверх ожидаемой версии.</summary>
        public async Task<CloudSaveVersion_> SaveSnapshotAsync(
            CloudSaveSnapshot_ snapshot,
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

            return new CloudSaveVersion_(serverRevision);
        }
    }
}
