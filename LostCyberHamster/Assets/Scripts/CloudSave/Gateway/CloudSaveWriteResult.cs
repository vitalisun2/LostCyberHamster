using System;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Подтверждённые сервером метаданные записанного снимка.
    /// </summary>
    public sealed class CloudSaveWriteResult
    {
        public CloudSaveWriteResult(string serverRevision, DateTime serverModifiedAtUtc)
        {
            // Проверяем обязательную серверную revision.
            if (string.IsNullOrWhiteSpace(serverRevision))
            {
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));
            }

            // Фиксируем подтверждённые сервером метаданные.
            ServerRevision = serverRevision;
            ServerModifiedAtUtc = serverModifiedAtUtc.ToUniversalTime();
        }

        /// <summary>UGS write lock записанной версии.</summary>
        public string ServerRevision { get; }

        /// <summary>Время изменения записи по данным сервера.</summary>
        public DateTime ServerModifiedAtUtc { get; }
    }
}
