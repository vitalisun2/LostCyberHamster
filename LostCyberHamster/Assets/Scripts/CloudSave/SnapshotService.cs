using System;
using System.Collections.Generic;
using GameManagement.CloudSave.Models;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит durable pending отдельно для каждого игрока.</summary>
    public sealed class SnapshotService
    {
        /// <summary>Старый общий ключ снимка.</summary>
        private const string LegacySnapshotKey = "CloudSave_.PendingSnapshot";

        /// <summary>Разделяет снимки игроков.</summary>
        private const string SnapshotKeyPrefix = "CloudSave_.PendingSnapshot.";

        /// <summary>Кэширует независимые pending по владельцам.</summary>
        private readonly Dictionary<string, CloudSaveSnapshot> _snapshotsByPlayerId =
            new Dictionary<string, CloudSaveSnapshot>(StringComparer.Ordinal);

        public SnapshotService()
        {
            MigrateLegacySnapshot();
        }

        /// <summary>Возвращает pending указанного игрока.</summary>
        public CloudSaveSnapshot GetPending(string playerId)
        {
            var storageKey = GetStorageKey(playerId);
            if (_snapshotsByPlayerId.TryGetValue(playerId, out var cached))
                return cached;

            if (!PlayerPrefs.HasKey(storageKey))
                return null;

            try
            {
                // Проверяем содержимое и владельца выбранного слота.
                var snapshot = CloudSaveSnapshot.FromJson(
                    PlayerPrefs.GetString(storageKey));
                if (!string.Equals(
                        snapshot.PlayerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Pending snapshot owner mismatch.");
                }

                _snapshotsByPlayerId[playerId] = snapshot;
                return snapshot;
            }
            catch (Exception exception)
            {
                // Повреждённый слот не должен блокировать новые checkpoint.
                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
                _snapshotsByPlayerId.Remove(playerId);
                Debug.LogWarning(
                    $"[CloudSave] Invalid pending snapshot removed ({exception.GetType().Name}).");
                return null;
            }
        }

        /// <summary>Ставит снимок в очередь его владельца.</summary>
        public void SetPending(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var playerId = snapshot.PlayerId;
            PlayerPrefs.SetString(GetStorageKey(playerId), snapshot.ToJson());
            PlayerPrefs.Save();
            _snapshotsByPlayerId[playerId] = snapshot;
        }

        /// <summary>Удаляет pending указанного игрока.</summary>
        public void Clear(string playerId)
        {
            PlayerPrefs.DeleteKey(GetStorageKey(playerId));
            PlayerPrefs.Save();
            _snapshotsByPlayerId.Remove(playerId);
        }

        /// <summary>Удаляет pending, только если подтверждён тот же снимок.</summary>
        public bool ClearIfCurrent(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var current = GetPending(snapshot.PlayerId);
            if (!AreSameSnapshot(current, snapshot))
                return false;

            Clear(snapshot.PlayerId);
            return true;
        }

        /// <summary>Возвращает отдельный ключ игрока.</summary>
        public static string GetStorageKey(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException(
                    "Player ID must be provided.",
                    nameof(playerId));

            return SnapshotKeyPrefix + playerId;
        }

        /// <summary>Переносит старый общий pending в слот его владельца.</summary>
        private void MigrateLegacySnapshot()
        {
            if (!PlayerPrefs.HasKey(LegacySnapshotKey))
                return;

            CloudSaveSnapshot legacySnapshot;
            string legacyJson;
            try
            {
                // Определяем владельца до изменения durable данных.
                legacyJson = PlayerPrefs.GetString(LegacySnapshotKey);
                legacySnapshot = CloudSaveSnapshot.FromJson(legacyJson);
            }
            catch (Exception exception)
            {
                // Невалидный legacy payload нельзя безопасно привязать к игроку.
                PlayerPrefs.DeleteKey(LegacySnapshotKey);
                PlayerPrefs.Save();
                Debug.LogWarning(
                    $"[CloudSave] Invalid legacy pending snapshot removed ({exception.GetType().Name}).");
                return;
            }

            try
            {
                // Выбираем самый новый валидный снимок владельца.
                var current = GetPending(legacySnapshot.PlayerId);
                if (current == null || legacySnapshot.SavedAtUtc > current.SavedAtUtc)
                    SetPending(legacySnapshot);

                // Удаляем общий ключ только после durable записи владельца.
                PlayerPrefs.DeleteKey(LegacySnapshotKey);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[CloudSave] Legacy pending migration failed ({exception.GetType().Name}).");
            }
        }

        /// <summary>Сравнивает полное содержимое и владельца двух снимков.</summary>
        private static bool AreSameSnapshot(
            CloudSaveSnapshot first,
            CloudSaveSnapshot second)
        {
            return first != null &&
                   second != null &&
                   string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal) &&
                   string.Equals(
                       first.PlayerDataJson,
                       second.PlayerDataJson,
                       StringComparison.Ordinal) &&
                   first.SavedAtUtc == second.SavedAtUtc;
        }
    }
}
