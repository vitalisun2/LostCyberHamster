using System;

namespace GameManagement.CloudSave.Version
{
    /// <summary>Хранит версию, подтверждённую сервером.</summary>
    public sealed class CloudSaveVersion
    {
        public CloudSaveVersion(string serverRevision)
        {
            if (string.IsNullOrWhiteSpace(serverRevision))
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));

            ServerRevision = serverRevision;
        }

        /// <summary>Версия снимка на сервере.</summary>
        public string ServerRevision { get; }
    }
}
