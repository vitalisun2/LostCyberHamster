using System.Threading.Tasks;
using GameManagement.CloudSave_.Models;
using GameManagement.CloudSave_.Version;

namespace GameManagement.CloudSave_.Gateway
{
    /// <summary>Работает с полным снимком в облаке.</summary>
    public interface ICloudSaveGateway_
    {
        /// <summary>Возвращает облачный снимок или null.</summary>
        Task<CloudSaveReadResult_> LoadSnapshotAsync();

        /// <summary>Сохраняет снимок поверх ожидаемой версии.</summary>
        Task<CloudSaveVersion_> SaveSnapshotAsync(
            CloudSaveSnapshot_ snapshot,
            string expectedServerRevision);
    }
}
