using System;
using GameManagement.CloudSave.Models;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Предоставляет актуальный pending и совместимость со старыми сохранениями.</summary>
    public sealed class SnapshotService
    {
        private const string LegacySnapshotKey = "CloudSave_.PendingSnapshot";
        private const string SnapshotKeyPrefix = "CloudSave_.PendingSnapshot.";
        private const string JournalFeature = "cloud-legacy-pending";

        public CloudSaveSnapshot GetPending(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.", nameof(playerId));
            if (GameDataManager.OwnerPlayerId == playerId && GameDataManager.HasUnsyncedProgress)
                return new CloudSaveSnapshot(playerId, GameDataManager.GetSavedPlayerDataJson());
            if (GameDataManager.OwnerPlayerId == playerId && GameDataManager.LastSyncedRevision > 0)
                return null;
            var journal = GameDataManager.GetJournalJson(JournalFeature, playerId);
            if (!string.IsNullOrWhiteSpace(journal)) return ReadOwned(journal, playerId);
            var key = GetStorageKey(playerId);
            if (PlayerPrefs.HasKey(key)) return ReadOwned(PlayerPrefs.GetString(key), playerId);
            return PlayerPrefs.HasKey(LegacySnapshotKey)
                ? ReadOwned(PlayerPrefs.GetString(LegacySnapshotKey), playerId) : null;
        }

        public void SetPending(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            GameDataManager.ExecuteTechnicalTransaction(() =>
                GameDataManager.SetJournalJson(JournalFeature, snapshot.ToJson(), snapshot.PlayerId));
        }

        public void Clear(string playerId)
        {
            GameDataManager.ExecuteTechnicalTransaction(() =>
                GameDataManager.SetJournalJson(JournalFeature, null, playerId));
            // Удаляем старый слот только после принятия его владельца в новом durable хранилище.
            if (GameDataManager.OwnerPlayerId != playerId) return;
            PlayerPrefs.DeleteKey(GetStorageKey(playerId));
            if (PlayerPrefs.HasKey(LegacySnapshotKey) && ReadOwned(PlayerPrefs.GetString(LegacySnapshotKey), playerId) != null)
                PlayerPrefs.DeleteKey(LegacySnapshotKey);
            PlayerPrefs.Save();
        }

        public bool ClearIfCurrent(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var current = GetPending(snapshot.PlayerId);
            if (current == null || current.PlayerDataJson != snapshot.PlayerDataJson) return false;
            Clear(snapshot.PlayerId);
            return true;
        }

        public static string GetStorageKey(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.", nameof(playerId));
            return SnapshotKeyPrefix + playerId;
        }

        private static CloudSaveSnapshot ReadOwned(string json, string playerId)
        {
            try
            {
                var snapshot = CloudSaveSnapshot.FromJson(json);
                return snapshot.PlayerId == playerId ? snapshot : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CloudSave] Legacy pending preserved but unavailable ({exception.GetType().Name}).");
                return null;
            }
        }
    }
}
