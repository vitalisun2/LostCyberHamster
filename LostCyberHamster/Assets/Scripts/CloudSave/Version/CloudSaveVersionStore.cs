using System;
using UnityEngine;

namespace GameManagement.CloudSave.Version
{
    /// <summary>Хранит подтверждённую версию каждого игрока.</summary>
    public sealed class CloudSaveVersionStore : ICloudSaveVersionStore
    {
        /// <summary>Разделяет версии игроков.</summary>
        private const string StorageKeyPrefix = "CloudSave_.Version.";

        /// <summary>Проверяет, подтверждён ли снимок игрока.</summary>
        public bool HasConfirmedVersion(string playerId)
        {
            var storageKey = GetStorageKey(playerId);
            return PlayerPrefs.HasKey(storageKey) &&
                   !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(storageKey));
        }

        /// <summary>Возвращает подтверждённую версию снимка.</summary>
        public string GetConfirmedRevision(string playerId)
        {
            var storageKey = GetStorageKey(playerId);
            if (!PlayerPrefs.HasKey(storageKey))
                return null;

            var serverRevision = PlayerPrefs.GetString(storageKey);
            return string.IsNullOrWhiteSpace(serverRevision) ? null : serverRevision;
        }

        /// <summary>Запоминает подтверждённую версию снимка.</summary>
        public void SaveConfirmedVersion(string playerId, string serverRevision)
        {
            if (string.IsNullOrWhiteSpace(serverRevision))
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));

            PlayerPrefs.SetString(GetStorageKey(playerId), serverRevision);
            PlayerPrefs.Save();
        }

        /// <summary>Возвращает отдельный ключ игрока.</summary>
        private static string GetStorageKey(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));

            return StorageKeyPrefix + playerId;
        }
    }
}
