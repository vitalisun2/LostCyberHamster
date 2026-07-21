using System;
using System.Threading.Tasks;
using GameManagement.CloudSave;

namespace Assets.Tests.EditMode
{
    internal sealed class FakeCloudSaveGateway : ICloudSaveGateway
    {
        public Task<CloudSaveReadResult> LoadTask { get; set; } =
            Task.FromResult<CloudSaveReadResult>(null);

        public Task<CloudSaveWriteResult> SaveTask { get; set; } = Task.FromResult(
            new CloudSaveWriteResult(
                "server-revision",
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        public CloudSaveSnapshot SavedSnapshot { get; private set; }

        public int LoadCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task<CloudSaveReadResult> LoadSnapshotAsync()
        {
            LoadCallCount++;
            return LoadTask;
        }

        public Task<CloudSaveWriteResult> SaveSnapshotAsync(CloudSaveSnapshot snapshot)
        {
            SaveCallCount++;
            SavedSnapshot = snapshot;
            return SaveTask;
        }
    }
}
