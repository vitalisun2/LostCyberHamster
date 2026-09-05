using System;
using UnityEngine;

namespace GameManagement.CloudSave.Version
{
    /// <summary>Читает новую серверную базу и сохраняет доступ к legacy версиям владельцев.</summary>
    public sealed class CloudSaveVersionStore : ICloudSaveVersionStore
    {
        public bool HasConfirmedVersion(string playerId) => !string.IsNullOrWhiteSpace(GetConfirmedRevision(playerId));

        public string GetConfirmedRevision(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.", nameof(playerId));
            if (GameDataManager.OwnerPlayerId == playerId) return GameDataManager.BaseCloudRevision;
            var value = GameDataManager.GetJournalJson("cloud-base", playerId);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            value = PlayerPrefs.GetString("CloudSave_.Version." + playerId, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public void SaveConfirmedVersion(string playerId, string serverRevision)
        {
            if (string.IsNullOrWhiteSpace(serverRevision)) throw new ArgumentException("Revision is required.", nameof(serverRevision));
            if (GameDataManager.OwnerPlayerId == playerId)
                GameDataManager.SetCloudBaseRevision(serverRevision);
            else
                GameDataManager.ExecuteTechnicalTransaction(() =>
                    GameDataManager.SetJournalJson("cloud-base", serverRevision, playerId));
        }
    }
}
