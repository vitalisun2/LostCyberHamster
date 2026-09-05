using System;

namespace Vues.GameCore
{
    /// <summary>Связывает повтор операции с неизменным предложением магазина.</summary>
    [Serializable]
    public sealed class ShopPurchaseReceipt
    {
        public string RequestId;
        public int ItemId;
        public ResourceType PaymentType;
        public int Price;
        public ResourceType RewardType;
        public int Amount;

        public static ShopPurchaseReceipt Capture(string requestId, ShopItem item) => new()
        {
            RequestId = requestId,
            ItemId = item.id,
            PaymentType = item.resource,
            Price = item.price,
            RewardType = item.type,
            Amount = item.amount
        };

        public bool Matches(ShopItem item) => item != null && ItemId == item.id &&
            PaymentType == item.resource && Price == item.price &&
            RewardType == item.type && Amount == item.amount;
    }
}
