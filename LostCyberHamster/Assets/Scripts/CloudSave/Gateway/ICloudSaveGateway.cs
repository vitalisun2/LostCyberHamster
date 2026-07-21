using System.Threading.Tasks;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Загружает и сохраняет один полный снимок прогресса в облаке.
    /// </summary>
    public interface ICloudSaveGateway
    {
        /// <summary>
        /// Загружает полный снимок или возвращает null, если его ещё нет.
        /// </summary>
        Task<CloudSaveReadResult> LoadSnapshotAsync();

        /// <summary>
        /// Записывает снимок с проверкой его базовой серверной revision.
        /// </summary>
        Task<CloudSaveWriteResult> SaveSnapshotAsync(CloudSaveSnapshot snapshot);
    }
}
