using System;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Подтверждённые сервером метаданные записанного снимка.
    /// </summary>
    public sealed class CloudSaveWriteResult
    {
        /// <summary>Создаёт результат успешной серверной записи.</summary>
        public CloudSaveWriteResult(string serverRevision, DateTime serverModifiedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(serverRevision))
            {
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));
            }

            ServerRevision = serverRevision;
            ServerModifiedAtUtc = serverModifiedAtUtc.ToUniversalTime();
        }

        /// <summary>UGS write lock записанной версии.</summary>
        public string ServerRevision { get; }

        /// <summary>Время изменения записи по данным сервера.</summary>
        public DateTime ServerModifiedAtUtc { get; }
    }
}
