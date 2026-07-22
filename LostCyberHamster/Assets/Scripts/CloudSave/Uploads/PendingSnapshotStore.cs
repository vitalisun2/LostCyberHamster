using System;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит один durable pending отдельно для каждого Player ID.</summary>
    public static class PendingSnapshotStore
    {
        /// <summary>Префикс per-owner ключей durable pending в PlayerPrefs.</summary>
        private const string StorageKeyPrefix = "CloudSave.PendingSnapshot.";

        /// <summary>Шифрует и расшифровывает сохранённый snapshot payload.</summary>
        private static readonly ICryptoService CryptoService = new AesCryptoService();

        /// <summary>Возвращает сохранённый pending или null при его отсутствии.</summary>
        public static CloudSaveSnapshotDto Load(string playerId)
        {
            // Выбираем per-owner запись и быстро завершаем чтение при её отсутствии.
            var storageKey = GetStorageKey(playerId);
            if (!PlayerPrefs.HasKey(storageKey))
                return null;

            try
            {
                // Расшифровываем и проверяем полный snapshot перед возвратом в sync flow.
                var encrypted = PlayerPrefs.GetString(storageKey);
                var snapshot = CloudSaveSnapshotCodec.Deserialize(CryptoService.Decrypt(encrypted));
                if (string.IsNullOrWhiteSpace(snapshot.PlayerDataJson) ||
                    !string.Equals(snapshot.PlayerId, playerId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(snapshot.Revision))
                {
                    throw new InvalidOperationException("Pending snapshot metadata is incomplete.");
                }

                return snapshot;
            }
            catch (Exception exception)
            {
                // Удаляем повреждённую запись, чтобы она не блокировала следующие checkpoint.
                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
                Debug.LogWarning($"[CloudSave] Invalid durable pending removed ({exception.GetType().Name}).");
                return null;
            }
        }

        /// <summary>Заменяет durable pending полным снимком до его облачной отправки.</summary>
        public static void Save(CloudSaveSnapshotDto snapshot)
        {
            // Проверяем обязательные данные durable snapshot.
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshot.PlayerDataJson) ||
                string.IsNullOrWhiteSpace(snapshot.PlayerId) ||
                string.IsNullOrWhiteSpace(snapshot.Revision))
            {
                throw new ArgumentException("Pending snapshot metadata is incomplete.", nameof(snapshot));
            }

            // Шифруем и сохраняем snapshot под ключом его владельца.
            var encrypted = CryptoService.Encrypt(CloudSaveSnapshotCodec.Serialize(snapshot));
            PlayerPrefs.SetString(GetStorageKey(snapshot.PlayerId), encrypted);
            PlayerPrefs.Save();
        }

        /// <summary>Удаляет durable pending только при подтверждении той же локальной revision и владельца.</summary>
        public static bool ClearIfMatches(CloudSaveSnapshotDto confirmedSnapshot)
        {
            // Проверяем переданный сервером snapshot.
            if (confirmedSnapshot == null)
                throw new ArgumentNullException(nameof(confirmedSnapshot));

            // Сверяем владельца и локальную revision с актуальным durable pending.
            var storageKey = GetStorageKey(confirmedSnapshot.PlayerId);
            var current = Load(confirmedSnapshot.PlayerId);
            if (current == null ||
                !string.Equals(current.PlayerId, confirmedSnapshot.PlayerId, StringComparison.Ordinal) ||
                !string.Equals(current.Revision, confirmedSnapshot.Revision, StringComparison.Ordinal))
            {
                return false;
            }

            // Удаляем только подтверждённую актуальную запись.
            PlayerPrefs.DeleteKey(storageKey);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Возвращает PlayerPrefs key указанного владельца.</summary>
        public static string GetStorageKey(string playerId)
        {
            // Проверяем владельца перед построением per-owner ключа.
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));

            // Добавляем Player ID к стабильному storage prefix.
            return StorageKeyPrefix + playerId;
        }
    }
}
