using System;
using System.Collections.Generic;

namespace Vues.GameCore
{
    /// <summary>Хранит последние подтверждённые покупки текущего владельца.</summary>
    [Serializable]
    public sealed class ShopPurchaseJournal
    {
        public List<ShopPurchaseReceipt> Receipts = new();
    }
}
