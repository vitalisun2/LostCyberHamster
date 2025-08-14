using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameAds;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Vues.GameCore
{
    public static class ShopManager
    {
        private static List<ShopItem> _shopItems;

        // Get the list of shop items
        public async static Task<List<ShopItem>> GetShopItems()
        {
            var textAsset = await Addressables.LoadAssetAsync<TextAsset>("shopItems.json").Task;
            var json = textAsset.text;
            _shopItems = JsonUtility.FromJson<ShopItemList>(json).items;
            return _shopItems;
        }

        public static void BuyItem(ShopItem item)
        {
            if(ResourceType.Advertisement == item.resource)
            {
                WatchAdForItem(item);
                return;
            }

            if(!CanBuyItem(item))
            {
                return;
            }

            ResourceManager.SpendResource(item.resource, item.price);
            AddReward(item);
            return;
        }

        private static void WatchAdForItem(ShopItem item)
        {
            GameEventsManager.OnAdCompleted += () => HandleAdCompleted(item);
            GameEventsManager.ShowAd();
        }

        private static void HandleAdCompleted(ShopItem item)
        {
            AddReward(item);
            GameEventsManager.OnAdCompleted -= () => HandleAdCompleted(item);
        }

        private static void AddReward(ShopItem item)
        {
            ResourceManager.AddResource(item.type, item.amount);
            GameEventsManager.ItemBought(item.id, item.resource, item.price);
        }

        public static bool CanBuyItem(ShopItem item)
        {
            if(ResourceType.Advertisement == item.resource)
            {
                return true;
            }
            return ResourceManager.CanSpendResource(item.resource, item.price);
        }

        public static void OnEnable()
        {
        }

        public static void OnDisable()
        {
        }
    }
}