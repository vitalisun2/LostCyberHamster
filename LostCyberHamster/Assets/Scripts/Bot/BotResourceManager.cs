using Assets.Scripts.Gameplay;
using Vues.GameCore;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Управляет покупками ресурсов во время игры (энергия, ульта).
    /// Бот вызывает покупки напрямую через API игры, минуя UI.
    /// </summary>
    public class BotResourceManager
    {
        private const int EnergyPrice = 50;   // монет за 100 энергии
        private const int UltaPrice = 100;    // монет за 100% ульты

        /// <summary>Текущий баланс монет игрока.</summary>
        public int CurrentCoins => ResourceManager.GetCurrentBalance(ResourceType.Coins);

        /// <summary>Хватает ли монет на покупку энергии.</summary>
        public bool CanBuyEnergy() => ResourceManager.CanSpendResource(ResourceType.Coins, EnergyPrice);

        /// <summary>Хватает ли монет на покупку ульты.</summary>
        public bool CanBuyUlta() => ResourceManager.CanSpendResource(ResourceType.Coins, UltaPrice);

        /// <summary>
        /// Покупает 100 энергии за 50 монет.
        /// </summary>
        /// <returns>true если покупка состоялась.</returns>
        public bool BuyEnergy(Hamster hamster)
        {
            if (!CanBuyEnergy()) return false;

            ResourceManager.SpendResource(ResourceType.Coins, EnergyPrice);
            hamster.AddEnergy(100);
            GameEventsManager.EnergyAdded(100);
            return true;
        }

        /// <summary>
        /// Покупает 100% ульты за 100 монет.
        /// </summary>
        /// <returns>true если покупка состоялась.</returns>
        public bool BuyUlta(Hamster hamster)
        {
            if (!CanBuyUlta()) return false;

            ResourceManager.SpendResource(ResourceType.Coins, UltaPrice);
            hamster.AddUltaCharge(100);
            return true;
        }
    }
}
