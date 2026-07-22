using System;
using System.Threading.Tasks;
using GameManagement.CloudSave;

namespace Assets.Tests.EditMode
{
    internal sealed class FakeCloudSaveGateway : ICloudSaveGateway
    {
        private CloudSaveReadResult _savedCloudVersion;

        public Task<CloudSaveReadResult> LoadTask { get; set; }

        public Task<CloudSaveWriteResult> SaveTask { get; set; } = Task.FromResult(
            new CloudSaveWriteResult(
                "server-revision",
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        public CloudSaveSnapshotDto SavedSnapshot { get; private set; }

        public int LoadCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Action<CloudSaveSnapshotDto> SaveStarting { get; set; }

        public Task<CloudSaveReadResult> LoadSnapshotAsync()
        {
            LoadCallCount++;
            return LoadTask ?? Task.FromResult(_savedCloudVersion);
        }

        public async Task<CloudSaveWriteResult> SaveSnapshotAsync(CloudSaveSnapshotDto snapshot)
        {
            SaveCallCount++;
            SavedSnapshot = snapshot;
            SaveStarting?.Invoke(snapshot);
            var result = await SaveTask;
            if (result != null)
            {
                _savedCloudVersion = new CloudSaveReadResult(
                    CloudSaveSnapshotCodec.Deserialize(CloudSaveSnapshotCodec.Serialize(snapshot)),
                    result.ServerRevision,
                    result.ServerModifiedAtUtc);
            }

            return result;
        }
    }
}
