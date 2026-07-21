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
        public static CloudSaveSnapshot Capture(
            PlayerData source,
            string playerId,
            string revision = null,
            string baseRevision = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            }

            return new CloudSaveSnapshot
            {
                PlayerDataJson = source.ToJson(),
                PlayerId = playerId,
                Revision = revision,
                BaseRevision = baseRevision,
                SavedAtUtc = source.LastSaveDate
            };
        }

        /// <summary>Сериализует целый облачный снимок.</summary>
        public static string Serialize(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return JsonUtility.ToJson(snapshot);
        }

        /// <summary>Десериализует целый облачный снимок.</summary>
        public static CloudSaveSnapshot Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Snapshot JSON must be provided.", nameof(json));
            }

            var snapshot = JsonUtility.FromJson<CloudSaveSnapshot>(json);
            return snapshot ?? throw new InvalidOperationException("Snapshot JSON is invalid.");
        }

        /// <summary>Создаёт независимый PlayerData из payload снимка.</summary>
        public static PlayerData RestorePlayerData(CloudSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(snapshot.PlayerDataJson))
            {
                throw new InvalidOperationException("Snapshot has no player data.");
            }

            return PlayerData.FromJson(snapshot.PlayerDataJson);
        }
    }
}
