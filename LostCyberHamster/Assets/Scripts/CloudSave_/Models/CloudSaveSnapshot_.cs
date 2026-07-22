using System;
using UnityEngine;

namespace GameManagement.CloudSave_.Models
{
    /// <summary>Фиксирует прогресс игрока для облачной синхронизации.</summary>
    [Serializable]
    public sealed class CloudSaveSnapshot_
    {
        /// <summary>Владелец снимка.</summary>
        [SerializeField]
        private string _playerId;

        /// <summary>Прогресс в момент создания снимка.</summary>
        [SerializeField]
        private string _playerDataJson;

        public CloudSaveSnapshot_(string playerId, string playerDataJson)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(playerDataJson))
                throw new ArgumentException("Player data must be provided.", nameof(playerDataJson));

            _playerId = playerId;
            _playerDataJson = playerDataJson;
        }

        /// <summary>Владелец снимка.</summary>
        public string PlayerId => _playerId;

        /// <summary>Прогресс в момент создания снимка.</summary>
        public string PlayerDataJson => _playerDataJson;

        /// <summary>Готовит снимок для хранения в облаке.</summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        /// <summary>Восстанавливает снимок из облачного представления.</summary>
        public static CloudSaveSnapshot_ FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Snapshot JSON must be provided.", nameof(json));

            var snapshot = JsonUtility.FromJson<CloudSaveSnapshot_>(json);
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot._playerId) ||
                string.IsNullOrWhiteSpace(snapshot._playerDataJson))
                throw new InvalidOperationException("Snapshot JSON is invalid.");

            return new CloudSaveSnapshot_(snapshot._playerId, snapshot._playerDataJson);
        }
    }
}
