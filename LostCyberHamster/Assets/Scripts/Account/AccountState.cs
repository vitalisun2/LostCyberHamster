namespace LostCyberHamster.Account
{
    /// <summary>
    /// Перечисляет состояния текущей UGS-сессии и связи с внешним аккаунтом.
    /// </summary>
    public enum AccountState
    {
        Unknown,
        Guest,
        Linked,
        Offline,
        Error
    }
}
