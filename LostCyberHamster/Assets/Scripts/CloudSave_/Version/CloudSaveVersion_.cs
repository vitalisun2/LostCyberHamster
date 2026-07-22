using System;

namespace GameManagement.CloudSave_.Version
{
    /// <summary>Хранит версию, подтверждённую сервером.</summary>
    public sealed class CloudSaveVersion_
    {
        public CloudSaveVersion_(string serverRevision)
        {
            if (string.IsNullOrWhiteSpace(serverRevision))
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));

            ServerRevision = serverRevision;
        }

        /// <summary>Версия снимка на сервере.</summary>
        public string ServerRevision { get; }
    }
}
