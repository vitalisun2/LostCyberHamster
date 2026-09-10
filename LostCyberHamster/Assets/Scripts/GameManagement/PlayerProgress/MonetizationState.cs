using System;

namespace GameManagement
{
    /// <summary>Сохранённые ограничения монетизации текущего владельца профиля.</summary>
    [Serializable]
    public sealed class MonetizationState
    {
        public long LastShopRewardUtcTicks;
        public string LastWinId;
        public int LastWinBonusCoins;
        public string LastRewardedWinId;
        public string LastRevivedRunId;
        public bool NoAds;
        public bool StarterPackOwned;
        public System.Collections.Generic.List<string> PurchaseTransactionIds = new();
        public int VisitCount;
        public bool InterstitialVariantAssigned;
        public bool InterstitialVariant;
        public long InterstitialDayUtcTicks;
        public int InterstitialDayCount;
    }
}
