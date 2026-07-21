using System;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Полный снимок игрового прогресса и его облачные метаданные.
    /// </summary>
    [Serializable]
    public sealed class CloudSaveSnapshot
    {
        /// <summary>Зафиксированный JSON полного PlayerData.</summary>
        public string PlayerDataJson;

        /// <summary>UGS Player ID владельца снимка.</summary>
        public string PlayerId;

        /// <summary>Локальная revision снимка.</summary>
        public string Revision;

        /// <summary>Серверная revision, от которой создан снимок.</summary>
        public string BaseRevision;

        /// <summary>Время завершённого локального сохранения в UTC.</summary>
        public string SavedAtUtc;
    }
}
