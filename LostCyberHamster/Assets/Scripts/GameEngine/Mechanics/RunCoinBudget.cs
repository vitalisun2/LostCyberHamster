using Vues.GameCore;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>Даёт текущей попытке тратить свои монеты раньше постоянного кошелька.</summary>
    internal sealed class RunCoinBudget
    {
        public int GrossCoins => RunLootBuffer.GrossCoins;

        public int AvailableCoins => RunLootBuffer.AvailableCoins;

        public bool CanSpend(int price) => RunLootBuffer.CanSpendCoins(price);

        public bool TrySpend(int price) => RunLootBuffer.TrySpendCoins(price);
    }
}
