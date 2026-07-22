namespace GameManagement.CloudSave_.Version
{
    /// <summary>Хранит подтверждённую версию каждого игрока.</summary>
    public interface ICloudSaveVersionStore_
    {
        /// <summary>Проверяет, подтверждён ли снимок игрока.</summary>
        bool HasConfirmedVersion(string playerId);

        /// <summary>Возвращает подтверждённую версию снимка.</summary>
        string GetConfirmedRevision(string playerId);

        /// <summary>Запоминает подтверждённую версию снимка.</summary>
        void SaveConfirmedVersion(string playerId, string serverRevision);
    }
}
