namespace GamePurchases
{
    /// <summary>Данные проверки: чек передаётся только доверенному серверу, в логи не попадает.</summary>
    public sealed class PurchaseProof
    {
        public string ProductId;
        public string TransactionId;
        public string Receipt;
        public string AppleJws;
        public string ProfileId;
        public string OwnerPlayerId;
        public long Generation;
        public bool RestoreEntitlementOnly;
    }
}
