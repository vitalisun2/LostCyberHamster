using System;
using GameManagement;
using Vues.GameCore;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>Учитывает неизрасходованную добычу попытки внутри единого сохранённого баланса монет.</summary>
    internal sealed class RunCoinBudget
    {
        private readonly string _profileId = GameDataManager.ProfileId;
        private readonly long _generation = GameDataManager.Generation;
        private int _remainingCoins;

        private bool IsCurrent => ResourceManager.IsReady && _profileId == GameDataManager.ProfileId &&
                                  _generation == GameDataManager.Generation;
        public int AvailableCoins => IsCurrent
            ? Math.Min(_remainingCoins, Math.Max(0, ResourceManager.GetCurrentBalance(ResourceType.Coins))) : 0;

        /// <summary>Отмечает добычу; сам общий баланс пополняет существующий ResourceManager.</summary>
        public void RecordCollection(int amount)
        {
            if (!IsCurrent || amount <= 0) return;
            _remainingCoins += Math.Min(amount, int.MaxValue - _remainingCoins);
        }

        public bool CanSpend(int price) => IsCurrent && ResourceManager.CanSpendResource(ResourceType.Coins, price);

        /// <summary>Сохраняет одно списание: сначала доля забега, затем недостающая часть из накоплений.</summary>
        public bool TrySpend(int price)
        {
            if (!CanSpend(price)) return false;
            int fromRun = Math.Min(AvailableCoins, price);
            bool committed = false;

            // Добыча уже входит в общий кошелёк: списываем общую цену ровно один раз.
            GameDataManager.ExecuteTransaction(CheckpointReason.RunRefillPurchased, () =>
            {
                if (!IsCurrent || !ResourceManager.SpendResource(ResourceType.Coins, price, notify: false))
                    throw new InvalidOperationException("Run refill balance changed before purchase.");
            }, () =>
            {
                // Ошибка записи и служебный пропуск транзакции оставляют добычу и пополнение неизменными.
                if (!IsCurrent) return;
                _remainingCoins -= fromRun;
                committed = true;
            });
            return committed;
        }
    }
}
