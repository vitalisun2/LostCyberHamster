using System.Collections.Generic;
using UnityEngine.Purchasing;

namespace GamePurchases
{
    /// <summary>SKU и состав предложений; денежные цены поступают только из магазина.</summary>
    public static class PurchaseCatalog
    {
        public const string NoAds = "lch.no_ads";
        public const string Crystals10 = "lch.crystals_10";
        public const string Crystals30 = "lch.crystals_30";
        public const string Starter = "lch.starter_pack";
        public static List<ProductDefinition> Definitions() => new()
        {
            new(NoAds, ProductType.NonConsumable), new(Crystals10, ProductType.Consumable),
            new(Crystals30, ProductType.Consumable), new(Starter, ProductType.NonConsumable)
        };
        public static bool Contains(string sku) => sku == NoAds || sku == Crystals10 || sku == Crystals30 || sku == Starter;
        public static int Crystals(string sku) => sku == Crystals10 ? 10 : sku == Crystals30 ? 30 : sku == Starter ? 20 : 0;
        public static int Coins(string sku) => sku == Starter ? 100 : 0;
        public static bool IsConsumable(string sku) => sku == Crystals10 || sku == Crystals30;
    }
}
