using System.Linq;
using GameManagement;

namespace GamePurchases
{
    /// <summary>Открывает набор после открытия платного скина за очко развития.</summary>
    public static class StarterPackEligibility
    {
        public static bool IsVisible => GameDataManager.PlayerData?.Monetization?.StarterPackOwned == true ||
            SkinManager.IsCatalogLoaded && SkinManager.AvailableSkins.Any(skin => skin.Id != 0 && skin.Price > 0 &&
                GameDataManager.PlayerData?.UnlockedSkinIds?.Contains(skin.Id) == true);
    }
}
