namespace LostCyberHamster.Account
{
    /// <summary>
    /// Перечисляет возможные исходы привязки внешнего аккаунта.
    /// </summary>
    public enum AccountLinkStatus
    {
        Unknown,
        Success,
        AlreadyLinked,
        Failed
    }
}
