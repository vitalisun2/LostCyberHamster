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
        private const string SnapshotKey = "player_snapshot";

        /// <inheritdoc />
        public async Task<CloudSaveReadResult> LoadSnapshotAsync()
        {
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SnapshotKey });
            if (!loaded.TryGetValue(SnapshotKey, out var item))
            {
                return null;
            }

            if (item.Value == null ||
                string.IsNullOrWhiteSpace(item.WriteLock) ||
                !item.Modified.HasValue)
            {
                throw new InvalidOperationException("Cloud Save returned incomplete snapshot metadata.");
            }

            var snapshot = CloudSaveSnapshotCodec.Deserialize(item.Value.GetAsString());
            return new CloudSaveReadResult(snapshot, item.WriteLock, item.Modified.Value);
        }

        /// <inheritdoc />
        public async Task<CloudSaveWriteResult> SaveSnapshotAsync(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var writeLock = string.IsNullOrWhiteSpace(snapshot.BaseRevision)
                ? null
                : snapshot.BaseRevision;
            var data = new Dictionary<string, SaveItem>
            {
                [SnapshotKey] = new SaveItem(CloudSaveSnapshotCodec.Serialize(snapshot), writeLock)
            };

            var saved = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            if (!saved.TryGetValue(SnapshotKey, out var serverRevision) ||
                string.IsNullOrWhiteSpace(serverRevision))
            {
                throw new InvalidOperationException("Cloud Save did not return snapshot revision.");
            }

            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { SnapshotKey });
            if (!loaded.TryGetValue(SnapshotKey, out var item) ||
                !item.Modified.HasValue ||
                !string.Equals(item.WriteLock, serverRevision, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cloud Save did not confirm saved snapshot metadata.");
            }

            return new CloudSaveWriteResult(serverRevision, item.Modified.Value);
        }
    }
}
