using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит последнюю подтверждённую облачную версию отдельно для каждого Player ID.</summary>
    public static class ConfirmedVersionStore
    {
        /// <summary>Префикс per-owner ключей подтверждённой cloud version в PlayerPrefs.</summary>
        private const string StorageKeyPrefix = "CloudSave.ConfirmedVersion.";

        /// <summary>Возвращает подтверждённую версию указанного владельца.</summary>
        public static CloudSaveWriteResult Load(string playerId)
        {
            // Выбираем per-owner запись и быстро завершаем чтение при её отсутствии.
            var storageKey = GetStorageKey(playerId);
            if (!PlayerPrefs.HasKey(storageKey))
                return null;

            try
            {
                // Разбираем сохранённые server revision и UTC timestamp.
                var parts = PlayerPrefs.GetString(storageKey).Split('|');
                if (parts.Length != 2)
                    throw new InvalidOperationException("Confirmed version metadata is incomplete.");

                var serverRevision = Decode(parts[0]);
                var ticks = long.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture);
                return new CloudSaveWriteResult(
                    serverRevision,
                    new DateTime(ticks, DateTimeKind.Utc));
            }
            catch (Exception exception)
            {
                // Удаляем повреждённую запись, чтобы она не блокировала sync flow.
                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
                Debug.LogWarning($"[CloudSave] Invalid confirmed version removed ({exception.GetType().Name}).");
                return null;
            }
        }

        /// <summary>Сохраняет подтверждённые сервером revision и время указанного владельца.</summary>
        public static void Save(string playerId, CloudSaveWriteResult version)
        {
            // Проверяем подтверждённую сервером version.
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            // Сериализуем server revision и UTC timestamp.
            var serialized = string.Join(
                "|",
                Encode(version.ServerRevision),
                version.ServerModifiedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture));

            // Сохраняем metadata под ключом владельца.
            PlayerPrefs.SetString(GetStorageKey(playerId), serialized);
            PlayerPrefs.Save();
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

        /// <summary>Кодирует server revision для безопасного разделения полей.</summary>
        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>Декодирует сохранённую server revision.</summary>
        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
