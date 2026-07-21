using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит последний неподтверждённый облачный снимок между сессиями.</summary>
    public static class CloudPendingSnapshotStore
    {
        /// <summary>Ключ PlayerPrefs для durable pending.</summary>
        public const string StorageKey = "CloudSave.PendingSnapshot";

        /// <summary>Ключ PlayerPrefs последней подтверждённой облачной версии.</summary>
        public const string ConfirmedVersionStorageKey = "CloudSave.ConfirmedVersion";

        private static readonly ICryptoService CryptoService = new AesCryptoService();

        /// <summary>Возвращает сохранённый pending или null при его отсутствии.</summary>
        public static CloudSaveSnapshot Load()
        {
            if (!PlayerPrefs.HasKey(StorageKey))
                return null;

            try
            {
                // Читаем и проверяем полный снимок перед возвратом в очередь.
                var encrypted = PlayerPrefs.GetString(StorageKey);
                var snapshot = CloudSaveSnapshotCodec.Deserialize(CryptoService.Decrypt(encrypted));
                if (string.IsNullOrWhiteSpace(snapshot.PlayerDataJson) ||
                    string.IsNullOrWhiteSpace(snapshot.PlayerId) ||
                    string.IsNullOrWhiteSpace(snapshot.Revision))
                {
                    throw new InvalidOperationException("Pending snapshot metadata is incomplete.");
                }

                return snapshot;
            }
            catch (Exception exception)
            {
                // Повреждённое значение не должно блокировать последующие checkpoint.
                PlayerPrefs.DeleteKey(StorageKey);
                PlayerPrefs.Save();
                Debug.LogWarning($"[CloudSave] Invalid durable pending removed ({exception.GetType().Name}).");
                return null;
            }
        }

        /// <summary>Заменяет durable pending полным снимком до его облачной отправки.</summary>
        public static void Save(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var encrypted = CryptoService.Encrypt(CloudSaveSnapshotCodec.Serialize(snapshot));
            PlayerPrefs.SetString(StorageKey, encrypted);
            PlayerPrefs.Save();
        }

        /// <summary>Удаляет durable pending только при подтверждении той же локальной revision и владельца.</summary>
        public static bool ClearIfMatches(CloudSaveSnapshot confirmedSnapshot)
        {
            if (confirmedSnapshot == null)
                throw new ArgumentNullException(nameof(confirmedSnapshot));

            var current = Load();
            if (current == null ||
                !string.Equals(current.PlayerId, confirmedSnapshot.PlayerId, StringComparison.Ordinal) ||
                !string.Equals(current.Revision, confirmedSnapshot.Revision, StringComparison.Ordinal))
            {
                return false;
            }

            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Возвращает подтверждённую версию только для указанного владельца.</summary>
        public static CloudSaveWriteResult LoadConfirmedVersion(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId) ||
                !PlayerPrefs.HasKey(ConfirmedVersionStorageKey))
            {
                return null;
            }

            try
            {
                var parts = PlayerPrefs.GetString(ConfirmedVersionStorageKey).Split('|');
                if (parts.Length != 3)
                    throw new InvalidOperationException("Confirmed version metadata is incomplete.");

                var storedPlayerId = Decode(parts[0]);
                if (!string.Equals(storedPlayerId, playerId, StringComparison.Ordinal))
                    return null;

                var serverRevision = Decode(parts[1]);
                var ticks = long.Parse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture);
                return new CloudSaveWriteResult(
                    serverRevision,
                    new DateTime(ticks, DateTimeKind.Utc));
            }
            catch (Exception exception)
            {
                PlayerPrefs.DeleteKey(ConfirmedVersionStorageKey);
                PlayerPrefs.Save();
                Debug.LogWarning($"[CloudSave] Invalid confirmed version removed ({exception.GetType().Name}).");
                return null;
            }
        }

        /// <summary>Сохраняет владельца и подтверждённые сервером revision/time.</summary>
        public static void SaveConfirmedVersion(
            string playerId,
            CloudSaveWriteResult version)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            var serialized = string.Join(
                "|",
                Encode(playerId),
                Encode(version.ServerRevision),
                version.ServerModifiedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(ConfirmedVersionStorageKey, serialized);
            PlayerPrefs.Save();
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
