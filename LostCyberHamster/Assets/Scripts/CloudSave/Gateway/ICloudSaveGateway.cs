using System.Threading.Tasks;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;

namespace GameManagement.CloudSave.Gateway
{
    /// <summary>Работает с полным снимком в облаке.</summary>
    public interface ICloudSaveGateway
    {
        /// <summary>Возвращает облачный снимок или null.</summary>
        Task<CloudSaveReadResult> LoadSnapshotAsync();

        /// <summary>Сохраняет снимок поверх ожидаемой версии.</summary>
        Task<CloudSaveVersion> SaveSnapshotAsync(
            CloudSaveSnapshot snapshot,
            string expectedServerRevision);
    }
}
