using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Tutorial;
using GameAds;
using GameManagement;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GamePurchases
{
    /// <summary>Клиент IAP 5: подтверждает заказ только после доверенной сохранённой выдачи.</summary>
    public sealed class PurchaseService
    {
        private static PurchaseService _instance;
        public static PurchaseService Instance => _instance ??= new PurchaseService();
        private StoreController _store;
        private IPurchaseFulfillment _fulfillment;
        private readonly Dictionary<string, Product> _products = new();
        private readonly HashSet<string> _processing = new();
        private bool _connecting;
        private bool _restoring;
        private string _buying;
        private IDisposable _purchaseBlock;
        private string _purchaseProfile;
        private long _purchaseGeneration;
        public string StatusKey { get; private set; } = "iap_unavailable";
        public bool IsBusy => _connecting || _restoring || _buying != null || _processing.Count > 0;
        public event Action Changed;
        private bool IsConfigured => MonetizationConfig.Current.EnablePurchases && _fulfillment != null;
        public bool CanRestore => IsConfigured && CurrentProfileReady && !IsBusy && _store != null &&
            !GameDataManager.IsProfileReplacementBlocked;
        private bool CurrentProfileReady => GameDataManager.IsLoaded && !TutorialStorage.IsPlayerDataBackupActive &&
            !GameDataManager.HasProgressionTestingBackup && !AccountTransitionScope.IsActive &&
            !string.IsNullOrEmpty(GameDataManager.OwnerPlayerId);

        /// <summary>Подключается реализация проверенной серверной выдачи; без неё покупки закрыты.</summary>
        public void Configure(IPurchaseFulfillment fulfillment)
        {
            if (IsBusy) throw new InvalidOperationException("Purchase operation is in progress.");
            _fulfillment = fulfillment;
        }

        public string LocalizedPrice(string sku) => _products.TryGetValue(sku, out var product)
            ? product.metadata.localizedPriceString : null;
        public bool IsOwned(string sku) => sku == PurchaseCatalog.NoAds
            ? GameDataManager.PlayerData?.Monetization?.NoAds == true : sku == PurchaseCatalog.Starter &&
              GameDataManager.PlayerData?.Monetization?.StarterPackOwned == true;
        public bool CanBuy(string sku) => IsConfigured && CurrentProfileReady && !IsBusy &&
            !GameDataManager.IsProfileReplacementBlocked && !RewardedAdService.Instance.IsBusy &&
            !InterstitialAdService.Instance.IsBusy && !IsOwned(sku) &&
            (sku != PurchaseCatalog.Starter || StarterPackEligibility.IsVisible) &&
            _products.TryGetValue(sku, out var product) && product.availableToPurchase;

        public async Task ConnectAsync()
        {
            if (!IsConfigured || !CurrentProfileReady || _connecting) { Notify(); return; }
            _connecting = true;
            StatusKey = "iap_loading";
            Notify();
            try
            {
                if (_store == null)
                {
                    _store = UnityIAPServices.StoreController();
                    _store.ProcessPendingOrdersOnPurchasesFetched(false);
                    _store.OnPurchasePending += OnPending;
                    _store.OnPurchaseDeferred += OnDeferred;
                    _store.OnPurchaseFailed += OnFailed;
                    _store.OnProductsFetched += OnProducts;
                    _store.OnProductsFetchFailed += OnProductsFailed;
                    _store.OnPurchasesFetched += OnOrders;
                    _store.OnPurchasesFetchFailed += OnRestoreFailed;
                    _store.OnStoreDisconnected += OnDisconnected;
                }
                await _store.Connect();
                _store.FetchProductsWithNoRetries(PurchaseCatalog.Definitions());
            }
            catch (Exception exception)
            {
                _connecting = false;
                StatusKey = "iap_try_again";
                Debug.LogWarning($"[IAP] Connection failed: {exception.GetType().Name}.");
                Notify();
            }
        }

        public void Buy(string sku)
        {
            if (!CanBuy(sku)) return;
            try
            {
                string binding = AccountBinding(GameDataManager.OwnerPlayerId);
                _store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(binding);
                _store.AppleStoreExtendedService?.SetAppAccountToken(Guid.ParseExact(binding.Substring(0, 32), "N"));
            }
            catch { StatusKey = "iap_try_again"; Notify(); return; }
            MonetizationEvent.Record("purchase_choice", sku);
            _buying = sku;
            _purchaseProfile = GameDataManager.ProfileId;
            _purchaseGeneration = GameDataManager.Generation;
            _purchaseBlock = GameDataManager.AcquireProfileReplacementBlock();
            StatusKey = "iap_processing";
            Notify();
            try { _store.PurchaseProduct(_products[sku]); }
            catch (Exception exception)
            {
                // Исход native вызова может быть неизвестен; снимает блок только terminal callback.
                StatusKey = "iap_waiting_store";
                Debug.LogWarning($"[IAP] Purchase outcome unknown: {exception.GetType().Name}.");
                Notify();
            }
        }

        public void Restore()
        {
            if (!CanRestore) return;
            _restoring = true;
            MonetizationEvent.Record("restore_choice", "iap");
            StatusKey = "iap_restoring";
            Notify();
            try
            {
                _store.RestoreTransactions((success, error) =>
                {
                    if (success) _store.FetchPurchases();
                    else { _restoring = false; StatusKey = "iap_try_again"; Notify(); }
                });
            }
            catch { _restoring = false; StatusKey = "iap_try_again"; Notify(); }
        }

        private void OnProducts(List<Product> products)
        {
            _connecting = false;
            foreach (var product in products)
                if (PurchaseCatalog.Contains(product.definition.id)) _products[product.definition.id] = product;
            StatusKey = string.Empty;
            _restoring = true;
            _store.FetchPurchases();
            Notify();
        }
        private async void OnOrders(Orders orders)
        {
            _restoring = true;
            try
            {
                foreach (var order in orders.PendingOrders) await Process(order, restoreOnly: false);
                foreach (var order in orders.ConfirmedOrders) await Process(order, restoreOnly: true);
            }
            catch (Exception exception)
            {
                StatusKey = "iap_pending_verification";
                Debug.LogWarning($"[IAP] Restore remains pending: {exception.GetType().Name}.");
            }
            finally { _restoring = false; Notify(); }
        }
        private async void OnPending(PendingOrder order) => await Process(order, restoreOnly: false);

        private async Task Process(Order order, bool restoreOnly)
        {
            var items = order?.CartOrdered?.Items()?.ToArray();
            if (!IsConfigured || !CurrentProfileReady || items?.Length != 1 || items[0].Quantity != 1) return;
            string sku = items[0].Product.definition.id;
            string transaction = order.Info.TransactionID;
            if (!PurchaseCatalog.Contains(sku) || restoreOnly && PurchaseCatalog.IsConsumable(sku) ||
                string.IsNullOrEmpty(transaction)) return;
            if (_buying != null && (_purchaseProfile != GameDataManager.ProfileId ||
                _purchaseGeneration != GameDataManager.Generation)) return;
            string binding = AccountBinding(GameDataManager.OwnerPlayerId);
            bool matchesAccount = order.Info.Google != null
                ? order.Info.Google.ObfuscatedAccountId == binding
                : order.Info.Apple?.AppAccountToken == Guid.ParseExact(binding.Substring(0, 32), "N");
            if (!matchesAccount)
            {
                StatusKey = "iap_pending_verification";
                if (_buying == sku) ReleasePurchase();
                Notify();
                return;
            }
            string key = sku + ":" + transaction;
            if (!_processing.Add(key)) return;
            using var profileBlock = GameDataManager.AcquireProfileReplacementBlock();
            string profile = GameDataManager.ProfileId;
            long generation = GameDataManager.Generation;
            StatusKey = "iap_verifying";
            Notify();
            try
            {
                var proof = new PurchaseProof
                {
                    ProductId = sku, TransactionId = transaction, Receipt = order.Info.Receipt,
                    AppleJws = order.Info.Apple?.jwsRepresentation,
                    ProfileId = profile, OwnerPlayerId = GameDataManager.OwnerPlayerId, Generation = generation,
                    RestoreEntitlementOnly = restoreOnly
                };
                bool durable = await _fulfillment.FulfillAndPersistAsync(proof);
                if (!durable || profile != GameDataManager.ProfileId || generation != GameDataManager.Generation)
                { StatusKey = "iap_pending_verification"; return; }
                if (order is PendingOrder pending) _store.ConfirmPurchase(pending);
                StatusKey = restoreOnly ? "iap_restored" : "iap_received";
                MonetizationEvent.Record(restoreOnly ? "entitlement_restored" : "purchase_saved", sku, transaction);
            }
            catch (Exception exception)
            {
                StatusKey = "iap_pending_verification";
                Debug.LogWarning($"[IAP] Fulfillment remains pending: {exception.GetType().Name}.");
            }
            finally
            {
                _processing.Remove(key);
                if (_buying == sku) ReleasePurchase();
                Notify();
            }
        }
        private bool MatchesPurchase(Order order) => _buying != null &&
            order?.CartOrdered?.Items()?.Any(item => item.Product.definition.id == _buying) == true;
        private void OnDeferred(DeferredOrder order) { if (MatchesPurchase(order)) ReleasePurchase(); StatusKey = "iap_deferred"; MonetizationEvent.Record("purchase_deferred", "iap"); Notify(); }
        private void OnFailed(FailedOrder order) { if (MatchesPurchase(order)) ReleasePurchase(); StatusKey = "iap_try_again"; MonetizationEvent.Record("purchase_failed", "iap"); Notify(); }
        private void OnProductsFailed(ProductFetchFailed failure) { _connecting = false; StatusKey = "iap_try_again"; Notify(); }
        private void OnRestoreFailed(PurchasesFetchFailureDescription failure) { _restoring = false; StatusKey = "iap_try_again"; Notify(); }
        private void OnDisconnected(StoreConnectionFailureDescription failure)
        { _connecting = false; _products.Clear(); StatusKey = "iap_unavailable"; Notify(); }
        private static string AccountBinding(string owner)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("lch.iap.v1:" + owner)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }
        private void ReleasePurchase() { _buying = null; _purchaseBlock?.Dispose(); _purchaseBlock = null; }
        private void Notify()
        {
            if (Changed == null) return;
            foreach (Action callback in Changed.GetInvocationList())
                try { callback(); } catch (Exception exception) { Debug.LogException(exception); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            var current = _instance;
            if (current?._store != null)
            {
                current._store.OnPurchasePending -= current.OnPending;
                current._store.OnPurchaseDeferred -= current.OnDeferred;
                current._store.OnPurchaseFailed -= current.OnFailed;
                current._store.OnProductsFetched -= current.OnProducts;
                current._store.OnProductsFetchFailed -= current.OnProductsFailed;
                current._store.OnPurchasesFetched -= current.OnOrders;
                current._store.OnPurchasesFetchFailed -= current.OnRestoreFailed;
                current._store.OnStoreDisconnected -= current.OnDisconnected;
            }
            current?.ReleasePurchase();
            _instance = null;
        }
    }
}
