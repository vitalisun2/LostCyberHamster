using System;
using System.Globalization;
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

        /// <summary>Время создания снимка.</summary>
        [SerializeField]
        private string _savedAtUtc;

        public CloudSaveSnapshot_(string playerId, string playerDataJson)
            : this(
                playerId,
                playerDataJson,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
        {
        }

        private CloudSaveSnapshot_(
            string playerId,
            string playerDataJson,
            string savedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(playerDataJson))
                throw new ArgumentException("Player data must be provided.", nameof(playerDataJson));

            _playerId = playerId;
            _playerDataJson = playerDataJson;
            _savedAtUtc = savedAtUtc;
        }

        /// <summary>Владелец снимка.</summary>
        public string PlayerId => _playerId;

        /// <summary>Прогресс в момент создания снимка.</summary>
        public string PlayerDataJson => _playerDataJson;

        /// <summary>Время создания снимка.</summary>
        public DateTime SavedAtUtc => DateTime.Parse(
                _savedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
            .ToUniversalTime();

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
                string.IsNullOrWhiteSpace(snapshot._playerDataJson) ||
                !DateTime.TryParse(
                    snapshot._savedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                throw new InvalidOperationException("Snapshot JSON is invalid.");
            }

            return new CloudSaveSnapshot_(
                snapshot._playerId,
                snapshot._playerDataJson,
                snapshot._savedAtUtc);
        }
    }
}
