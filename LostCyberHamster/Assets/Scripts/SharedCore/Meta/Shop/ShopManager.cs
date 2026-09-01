using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Vues.GameCore
{
    public static class ShopManager
    {
        private static List<ShopItem> _shopItems;
        private static ShopItem _pendingAdvertisementItem;
        private static bool _isAdvertisementPending;

        public static async Task<List<ShopItem>> GetShopItems()
        {
            // Загружаем каталог только на время чтения JSON.
            var handle = Addressables.LoadAssetAsync<TextAsset>(
                "shopItems.json");
            try
            {
                var textAsset = await handle.Task;
                var itemList = JsonUtility.FromJson<ShopItemList>(
                    textAsset.text);
                _shopItems = itemList.items;
                return _shopItems;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        public static void BuyItem(ShopItem item)
        {
            // Маршрутизируем рекламное предложение в активный показ.
            if (ResourceType.Advertisement == item.resource)
            {
                WatchAdForItem(item);
                return;
            }

            // Списываем цену и выдаём награду обычной покупки.
            if (!CanBuyItem(item))
            {
                return;
            }

            ResourceManager.SpendResource(item.resource, item.price);
            AddReward(item);
        }

        private static void WatchAdForItem(ShopItem item)
        {
            // Одновременно удерживаем только одно рекламное предложение.
            if (_isAdvertisementPending)
            {
                return;
            }

            // Сохраняем награду до получения итогового статуса рекламы.
            _pendingAdvertisementItem = item;
            _isAdvertisementPending = true;
            GameEventsManager.OnAdFinished += HandleAdFinished;
            GameEventsManager.ShowAd();
        }

        private static void HandleAdFinished(bool completed)
        {
            // Освобождаем рекламную операцию при любом результате показа.
            var item = _pendingAdvertisementItem;
            _pendingAdvertisementItem = null;
            _isAdvertisementPending = false;
            GameEventsManager.OnAdFinished -= HandleAdFinished;

            // Выдаём награду только за полностью просмотренную рекламу.
            if (completed && item != null)
            {
                AddReward(item);
            }
        }

        private static void AddReward(ShopItem item)
        {
            ResourceManager.AddResource(item.type, item.amount);
            GameEventsManager.ItemBought(item.id, item.resource, item.price);
        }

        public static bool CanBuyItem(ShopItem item)
        {
            if (ResourceType.Advertisement == item.resource)
            {
                return !_isAdvertisementPending;
            }

            return ResourceManager.CanSpendResource(item.resource, item.price);
        }
    }
}
