using System;
using Assets.Scripts.Diagnostics;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            GameEventsManager.OnCoinCollected += RecordRunCoins;
            GameEventsManager.OnCrystalsCollected += RecordRunCrystals;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnCoinCollected -= RecordRunCoins;
            GameEventsManager.OnCrystalsCollected -= RecordRunCrystals;
        }

        private static void RecordRunCoins(int amount)
        {
            RunLootBuffer.RecordCollection(ResourceType.Coins, amount, GameEventsManager.CollectionSource);
        }

        private static void RecordRunCrystals(int amount)
        {
            RunLootBuffer.RecordCollection(ResourceType.Crystals, amount, GameEventsManager.CollectionSource);
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

    /// <summary>
    /// Держит добычу активной production-попытки вне постоянного кошелька до победы.
    /// </summary>
    public static class RunLootBuffer
    {
        public static event Action Changed;

        private static string _profileId;
        private static long _generation;
        private static int _sceneHandle;
        private static string _levelKey;
        private static int _coins;
        private static int _crystals;
        private static int _grossCoins;

        public static int AvailableCoins => IsCurrent ? _coins : 0;
        public static int AvailableCrystals => IsCurrent ? _crystals : 0;
        public static int GrossCoins => IsCurrent ? _grossCoins : 0;
        public static bool HasPendingLoot => AvailableCoins > 0 || AvailableCrystals > 0;

        private static bool IsCurrent => ResourceManager.IsReady &&
                                         _profileId == GameDataManager.ProfileId &&
                                         _generation == GameDataManager.Generation &&
                                         _sceneHandle == SceneManager.GetActiveScene().handle &&
                                         string.Equals(_levelKey, GameDataManager.PlayerData?.CurrentLevel,
                                             StringComparison.Ordinal);

        public static void BeginAttempt(string levelKey)
        {
            _profileId = GameDataManager.ProfileId;
            _generation = GameDataManager.Generation;
            _sceneHandle = SceneManager.GetActiveScene().handle;
            _levelKey = levelKey;
            _coins = 0;
            _crystals = 0;
            _grossCoins = 0;
            NotifyChanged();
        }

        public static void Discard()
        {
            if (_coins == 0 && _crystals == 0 && _grossCoins == 0 && string.IsNullOrEmpty(_levelKey))
            {
                return;
            }

            _coins = 0;
            _crystals = 0;
            _grossCoins = 0;
            _profileId = null;
            _generation = 0;
            _sceneHandle = 0;
            _levelKey = null;
            NotifyChanged();
        }

        public static bool RecordCollection(ResourceType resourceType, int amount, string source)
        {
            if (!IsCurrent || amount <= 0)
            {
                return false;
            }

            switch (resourceType)
            {
                case ResourceType.Coins:
                    _coins = AddWithoutOverflow(_coins, amount);
                    _grossCoins = AddWithoutOverflow(_grossCoins, amount);
                    EconomyTelemetry.Collection("coins", amount, source);
                    NotifyChanged();
                    return true;
                case ResourceType.Crystals:
                    _crystals = AddWithoutOverflow(_crystals, amount);
                    EconomyTelemetry.Collection("crystals", amount, source);
                    NotifyChanged();
                    return true;
                default:
                    return false;
            }
        }

        public static bool CanSpendCoins(int price)
        {
            return IsCurrent && price > 0 && AvailableCoins + ResourceManager.GetCurrentBalance(ResourceType.Coins) >= price;
        }

        public static bool TrySpendCoins(int price)
        {
            if (!CanSpendCoins(price))
            {
                return false;
            }

            int fromRun = Math.Min(AvailableCoins, price);
            int fromWallet = price - fromRun;

            if (fromWallet == 0)
            {
                _coins -= fromRun;
                NotifyChanged();
                return true;
            }

            bool committed = false;
            GameDataManager.ExecuteTransaction(CheckpointReason.RunRefillPurchased, () =>
            {
                if (!IsCurrent || !ResourceManager.SpendResource(ResourceType.Coins, fromWallet, notify: false))
                {
                    throw new InvalidOperationException("Run refill balance changed before purchase.");
                }
            }, () =>
            {
                if (!IsCurrent)
                {
                    return;
                }

                _coins -= fromRun;
                committed = true;
                NotifyChanged();
            });
            return committed;
        }

        public static void CommitToPersistentWallet()
        {
            if (!IsCurrent)
            {
                return;
            }

            if (_coins > 0 && !ResourceManager.AddResource(ResourceType.Coins, _coins, notify: false))
            {
                throw new InvalidOperationException("Cannot commit run coins to persistent wallet.");
            }

            if (_crystals > 0 && !ResourceManager.AddResource(ResourceType.Crystals, _crystals, notify: false))
            {
                throw new InvalidOperationException("Cannot commit run crystals to persistent wallet.");
            }
        }

        public static void MarkCommitted()
        {
            Discard();
        }

        private static int AddWithoutOverflow(int current, int amount)
        {
            return current + Math.Min(amount, int.MaxValue - current);
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
