using System;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит последний неподтверждённый облачный снимок между сессиями.</summary>
    public static class CloudPendingSnapshotStore
    {
        /// <summary>Ключ PlayerPrefs для durable pending.</summary>
        public const string StorageKey = "CloudSave.PendingSnapshot";

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
    }
}
