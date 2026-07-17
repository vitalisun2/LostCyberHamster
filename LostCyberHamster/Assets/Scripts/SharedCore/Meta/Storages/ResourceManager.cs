using GameManagement;

namespace Vues.GameCore
{
    /// <summary>
    /// Единая точка чтения и изменения ресурсов в данных игрока.
    /// </summary>
    public static class ResourceManager
    {
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

        public static void AddResource(ResourceType resourceType, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            switch (resourceType)
            {
                case ResourceType.Crystals:
                    GameDataManager.PlayerData.Crystals += amount;
                    break;
                case ResourceType.Coins:
                    GameDataManager.PlayerData.Money += amount;
                    break;
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

        public static void SetResourceBalance(ResourceType resourceType, int balance)
        {
            switch (resourceType)
            {
                case ResourceType.Crystals:
                    GameDataManager.PlayerData.Crystals = balance;
                    break;
                case ResourceType.Coins:
                    GameDataManager.PlayerData.Money = balance;
                    break;
            }
        }

        public static bool SpendResource(ResourceType resourceType, int amount)
        {
            if (!CanSpendResource(resourceType, amount))
            {
                return false;
            }

            switch (resourceType)
            {
                case ResourceType.Crystals:
                    GameDataManager.PlayerData.Crystals -= amount;
                    return true;
                case ResourceType.Coins:
                    GameDataManager.PlayerData.Money -= amount;
                    return true;
                default:
                    return false;
            }
        }

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
    }
}
