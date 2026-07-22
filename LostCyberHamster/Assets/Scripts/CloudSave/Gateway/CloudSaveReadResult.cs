using System;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Загруженный снимок и его подтверждённые сервером метаданные.
    /// </summary>
    public sealed class CloudSaveReadResult
    {
        public CloudSaveReadResult(
            CloudSaveSnapshotDto snapshot,
            string serverRevision,
            DateTime serverModifiedAtUtc)
        {
            // Проверяем обязательные данные ответа.
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(serverRevision))
            {
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));
            }

            // Фиксируем подтверждённые сервером метаданные.
            ServerRevision = serverRevision;
            ServerModifiedAtUtc = serverModifiedAtUtc.ToUniversalTime();
        }

        /// <summary>Полный загруженный снимок.</summary>
        public CloudSaveSnapshotDto Snapshot { get; }

        /// <summary>UGS write lock загруженной версии.</summary>
        public string ServerRevision { get; }

        /// <summary>Время изменения записи по данным сервера.</summary>
        public DateTime ServerModifiedAtUtc { get; }
    }
}
