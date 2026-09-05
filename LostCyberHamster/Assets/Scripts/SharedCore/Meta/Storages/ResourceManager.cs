using System;
using GameManagement;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Единая точка чтения и изменения ресурсов в данных игрока.
    /// </summary>
    public static class ResourceManager
    {
        /// <summary>
        /// Возникает после успешного изменения баланса ресурса.
        /// </summary>
        public static event Action<ResourceType, int> BalanceChanged;

        public static bool IsReady => GameDataManager.PlayerData != null;

        public static bool CanSpendResource(ResourceType resourceType, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            switch (resourceType)
            {
                case ResourceType.Crystals:
                    return GameDataManager.PlayerData.Crystals >= amount;
                case ResourceType.Coins:
                    return GameDataManager.PlayerData.Money >= amount;
                default:
                    return false;
            }
        }

        public static bool AddResource(ResourceType resourceType, int amount, bool notify = true)
        {
            if (amount <= 0)
            {
                return false;
            }

            switch (resourceType)
            {
                case ResourceType.Crystals:
                    if (GameDataManager.PlayerData.Crystals > int.MaxValue - amount)
                    {
                        return false;
                    }

                    GameDataManager.PlayerData.Crystals += amount;
                    if (notify) NotifyBalanceChanged(resourceType);
                    return true;
                case ResourceType.Coins:
                    if (GameDataManager.PlayerData.Money > int.MaxValue - amount)
                    {
                        return false;
                    }

                    GameDataManager.PlayerData.Money += amount;
                    if (notify) NotifyBalanceChanged(resourceType);
                    return true;
                default:
                    return false;
            }
        }


        public static int GetCurrentBalance(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Crystals:
                    return GameDataManager.PlayerData.Crystals;
                case ResourceType.Coins:
                    return GameDataManager.PlayerData.Money;
                default:
                    return 0;
            }
        }

        public static bool SetResourceBalance(ResourceType resourceType, int balance, bool notify = true)
        {
            if (balance < 0)
            {
                return false;
            }

            switch (resourceType)
            {
                case ResourceType.Crystals:
                    GameDataManager.PlayerData.Crystals = balance;
                    if (notify) NotifyBalanceChanged(resourceType);
                    return true;
                case ResourceType.Coins:
                    GameDataManager.PlayerData.Money = balance;
                    if (notify) NotifyBalanceChanged(resourceType);
                    return true;
                default:
                    return false;
            }
        }

        public static bool SpendResource(ResourceType resourceType, int amount, bool notify = true)
        {
            if (!CanSpendResource(resourceType, amount))
            {
                return false;
            }

            switch (resourceType)
            {
                case ResourceType.Crystals:
                    GameDataManager.PlayerData.Crystals -= amount;
                    if (notify) NotifyBalanceChanged(resourceType);
                    return true;
                case ResourceType.Coins:
                    GameDataManager.PlayerData.Money -= amount;
                    if (notify) NotifyBalanceChanged(resourceType);
                    return true;
                default:
                    return false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Начисляет Money через production resource, checkpoint и economy event flow.
        /// </summary>
        public static bool TryAddMoneyForDevelopment(
            int amount,
            out int newBalance)
        {
            newBalance = IsReady
                ? GetCurrentBalance(ResourceType.Coins)
                : 0;
            if (!IsReady || !AddResource(ResourceType.Coins, amount))
                return false;

            PlayerProgressCommitter.Commit(
                CheckpointReason.DeveloperResourceGranted);
            GameEventsManager.EarnCoins(amount);
            newBalance = GetCurrentBalance(ResourceType.Coins);
            return true;
        }
#endif

        public static void OnEnable()
        {
            OnDisable();
            GameEventsManager.OnCoinCollected += AddCoins;
            GameEventsManager.OnCrystalsCollected += AddCrystals;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnCoinCollected -= AddCoins;
            GameEventsManager.OnCrystalsCollected -= AddCrystals;
        }

        private static void AddCoins(int amount)
        {
            AddResource(ResourceType.Coins, amount);
        }

        private static void AddCrystals(int amount)
        {
            AddResource(ResourceType.Crystals, amount);
        }

        private static void NotifyBalanceChanged(ResourceType resourceType)
        {
            BalanceChanged?.Invoke(
                resourceType,
                GetCurrentBalance(resourceType));
        }

        /// <summary>Обновляет UI после атомарной записи нескольких балансов.</summary>
        public static void NotifyBalancesChangedAfterCommit()
        {
            var handlers = BalanceChanged;
            if (handlers == null)
                return;
            foreach (Action<ResourceType, int> handler in handlers.GetInvocationList())
            {
                // Ошибка одного виджета не мешает остальным увидеть сохранённый баланс.
                foreach (var resource in new[] { ResourceType.Coins, ResourceType.Crystals })
                {
                    try { handler(resource, GetCurrentBalance(resource)); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
        }
    }
}
