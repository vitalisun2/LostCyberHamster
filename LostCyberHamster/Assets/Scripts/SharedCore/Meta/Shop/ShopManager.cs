using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameAds;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Vues.GameCore
{
    /// <summary>Проводит локальные покупки и передаёт rewarded операции владельцу SDK.</summary>
    public static class ShopManager
    {
        private const string PurchaseJournalFeature = "shop-purchases";
        private const int RecentPurchaseLimit = 64;

        public static async Task<List<ShopItem>> GetShopItems()
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>("shopItems.json");
            try
            {
                var textAsset = await handle.Task;
                return JsonUtility.FromJson<ShopItemList>(textAsset.text).items;
            }
            finally { Addressables.Release(handle); }
        }

        /// <summary>Сохраняет списание и награду вместе; уведомляет UI после записи.</summary>
        public static bool BuyItem(ShopItem item, string requestId = null)
        {
            if (item == null || !GameDataManager.IsLoaded)
                return false;
            if (item.resource == ResourceType.Advertisement)
                return CanBuyItem(item) && RewardedAdService.Instance.RequestShop(item) != null;

            requestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId.Trim();
            if (Guid.TryParse(requestId, out var parsedRequestId))
                requestId = parsedRequestId.ToString("N");
            try
            {
                var json = GameDataManager.GetJournalJson(PurchaseJournalFeature);
                var journal = string.IsNullOrWhiteSpace(json) ? new ShopPurchaseJournal() :
                    JsonUtility.FromJson<ShopPurchaseJournal>(json);
                if (journal?.Receipts == null)
                    throw new InvalidOperationException("Shop purchase journal is invalid.");
                var applied = journal.Receipts.Find(receipt => receipt != null && receipt.RequestId == requestId);
                if (applied != null)
                    return applied.Matches(item);
                if (!CanBuyItem(item))
                    return false;

                // Receipt, списание и награда сохраняются одним envelope до UI-событий.
                GameDataManager.ExecuteTransaction(CheckpointReason.ShopItemPurchased, () =>
                {
                    if (!CanBuyItem(item))
                        throw new InvalidOperationException("Shop offer is no longer affordable.");
                    if (!ResourceManager.SpendResource(item.resource, item.price, notify: false) ||
                        !ResourceManager.AddResource(item.type, item.amount, notify: false))
                        throw new InvalidOperationException("Shop balances could not be changed.");
                    journal.Receipts.Add(ShopPurchaseReceipt.Capture(requestId, item));
                    if (journal.Receipts.Count > RecentPurchaseLimit)
                        journal.Receipts.RemoveRange(0, journal.Receipts.Count - RecentPurchaseLimit);
                    GameDataManager.SetJournalJson(PurchaseJournalFeature, JsonUtility.ToJson(journal));
                }, () =>
                {
                    ResourceManager.NotifyBalancesChangedAfterCommit();
                    GameEventsManager.ItemBought(item.id, item.resource, item.price);
                });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Shop] Purchase rolled back: {exception.GetType().Name}.");
                return false;
            }
        }

        public static bool CanBuyItem(ShopItem item)
        {
            if (item == null || !GameDataManager.IsLoaded || item.amount <= 0 ||
                (item.type != ResourceType.Coins && item.type != ResourceType.Crystals))
                return false;
            long balanceAfterPayment = ResourceManager.GetCurrentBalance(item.type);
            if (item.resource == item.type)
                balanceAfterPayment -= item.price;
            if (balanceAfterPayment + item.amount > int.MaxValue)
                return false;
            if (item.resource == ResourceType.Advertisement)
                return RewardedAdService.Instance.CanRequest;
            return item.price > 0 && ResourceManager.CanSpendResource(item.resource, item.price);
        }

    }
}
