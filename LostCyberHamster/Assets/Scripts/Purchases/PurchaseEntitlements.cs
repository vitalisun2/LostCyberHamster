using System;
using System.Collections.Generic;
using GameManagement;
using Vues.GameCore;

namespace GamePurchases
{
    /// <summary>Локальная часть доверенной серверной выдачи в существующий кошелёк.</summary>
    public static class PurchaseEntitlements
    {
        /// <summary>
        /// Вызывается реализацией IPurchaseFulfillment после серверной проверки и cloud claim.
        /// transactionKey выдаёт сервер: store + уникальная транзакция. Cloud ledger переносится
        /// вместе с профилем, включая этот ID, чтобы восстановление не повторяло выданную валюту.
        /// </summary>
        public static bool ApplyVerified(PurchaseProof proof, string transactionKey)
        {
            if (proof == null || !PurchaseCatalog.Contains(proof.ProductId) ||
                string.IsNullOrWhiteSpace(transactionKey) || proof.ProfileId != GameDataManager.ProfileId ||
                proof.OwnerPlayerId != GameDataManager.OwnerPlayerId || proof.Generation != GameDataManager.Generation)
                throw new InvalidOperationException("Verified purchase owner does not match the loaded profile.");
            bool committed = false;
            bool balancesChanged = false;
            GameDataManager.ExecuteTransaction(CheckpointReason.PurchaseFulfilled, () =>
            {
                var state = GameDataManager.PlayerData.Monetization ??= new MonetizationState();
                state.PurchaseTransactionIds ??= new List<string>();
                bool applied = state.PurchaseTransactionIds.Contains(transactionKey);
                bool grant = !proof.RestoreEntitlementOnly &&
                    (proof.ProductId != PurchaseCatalog.Starter || !state.StarterPackOwned);
                if (proof.ProductId == PurchaseCatalog.NoAds) state.NoAds = true;
                if (proof.ProductId == PurchaseCatalog.Starter) state.StarterPackOwned = true;
                if (applied) return;
                if (grant)
                {
                    int coins = PurchaseCatalog.Coins(proof.ProductId);
                    int crystals = PurchaseCatalog.Crystals(proof.ProductId);
                    if (coins > 0 && !ResourceManager.AddResource(ResourceType.Coins, coins, notify: false) ||
                        crystals > 0 && !ResourceManager.AddResource(ResourceType.Crystals, crystals, notify: false))
                        throw new InvalidOperationException("Purchase reward cannot be added.");
                    balancesChanged = coins > 0 || crystals > 0;
                }
                state.PurchaseTransactionIds.Add(transactionKey);
            }, () => committed = true);
            if (committed && balancesChanged) ResourceManager.NotifyBalancesChangedAfterCommit();
            return committed;
        }
    }
}
