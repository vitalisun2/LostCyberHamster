namespace Assets.Scripts.Account
{
    /// <summary>
    /// Результат попытки привязать текущего гостя к способу входа.
    /// </summary>
    public enum AccountLinkResult
    {
        Linked,
        Cancelled,
        Conflict,
        Failed
    }
}
