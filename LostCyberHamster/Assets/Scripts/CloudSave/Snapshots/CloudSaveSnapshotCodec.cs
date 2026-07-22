using System;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Создаёт глубокую копию PlayerData и преобразует облачный снимок в JSON и обратно.
    /// </summary>
    public static class CloudSaveSnapshotCodec
    {
        /// <summary>
        /// Фиксирует текущий PlayerData и метаданные в независимом снимке.
        /// </summary>
        public static CloudSaveSnapshotDto Capture(
            PlayerData source,
            string playerId,
            string revision = null,
            string baseRevision = null)
        {
            // Проверяем источник и владельца снимка.
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            }

            // Фиксируем полный payload и метаданные текущего сохранения.
            return new CloudSaveSnapshotDto
            {
                PlayerDataJson = source.ToJson(),
                PlayerId = playerId,
                Revision = revision,
                BaseRevision = baseRevision,
                SavedAtUtc = source.LastSaveDate
            };
        }

        /// <summary>Сериализует целый облачный снимок.</summary>
        public static string Serialize(CloudSaveSnapshotDto snapshot)
        {
            // Проверяем входной снимок.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // Преобразуем снимок в JSON.
            return JsonUtility.ToJson(snapshot);
        }

        /// <summary>Десериализует целый облачный снимок.</summary>
        public static CloudSaveSnapshotDto Deserialize(string json)
        {
            // Проверяем входной JSON.
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Snapshot JSON must be provided.", nameof(json));
            }

            // Восстанавливаем снимок и отклоняем невалидный JSON.
            var snapshot = JsonUtility.FromJson<CloudSaveSnapshotDto>(json);
            return snapshot ?? throw new InvalidOperationException("Snapshot JSON is invalid.");
        }

        /// <summary>Создаёт независимый PlayerData из payload снимка.</summary>
        public static PlayerData RestorePlayerData(CloudSaveSnapshotDto snapshot)
        {
            // Проверяем снимок и наличие payload.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(snapshot.PlayerDataJson))
            {
                throw new InvalidOperationException("Snapshot has no player data.");
            }

            // Восстанавливаем независимые игровые данные.
            return PlayerData.FromJson(snapshot.PlayerDataJson);
        }
    }
}
