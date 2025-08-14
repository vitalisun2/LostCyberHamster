namespace Vues.GameCore
{
    /// <summary>
    /// Общее хранилище ресурсов.
    /// </summary>
    public static class ResourceManager
    {
        public static bool CanSpendResource(ResourceType resourceType, int amount)
        {
            switch (resourceType)
            {
                case ResourceType.Crystals:
                    return CrystalStorage.CanSpendCrystals(amount);
                case ResourceType.Coins:
                    return MoneyStorage.CanSpendMoney(amount);
                default:
                    return false;
            }
        }

        public static void AddResource(ResourceType resourceType, int amount)
        {
            switch (resourceType)
            {
                case ResourceType.Crystals:
                    CrystalStorage.AddCrystals(amount);
                    break;
                case ResourceType.Coins:
                    MoneyStorage.AddMoney(amount);
                    break;
            }
        }


        public static int GetCurrentBalance(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Crystals:
                    return CrystalStorage.GetCurrentBalance();
                case ResourceType.Coins:
                    return MoneyStorage.GetCurrentBalance();
                default:
                    return 0;
            }
        }

        public static bool SpendResource(ResourceType resourceType, int amount)
        {
            switch (resourceType)
            {
                case ResourceType.Crystals:
                    return CrystalStorage.SpendCrystals(amount);
                case ResourceType.Coins:
                    return MoneyStorage.SpendMoney(amount);
                default:
                    return false;
            }
        }
    }
}