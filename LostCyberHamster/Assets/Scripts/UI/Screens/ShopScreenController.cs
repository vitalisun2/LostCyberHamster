using System;
using System.Linq;
using System.Threading.Tasks;
using GameAds;
using GameManagement;
using GamePurchases;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Управляет рекламной наградой и покупкой кристаллов в полноэкранном магазине.
    /// </summary>
    public sealed class ShopScreenController : ScreenController
    {
        private const float DesignWidth = 1725f;
        private const float DesignHeight = 912f;

        private ShopItem _freeCoinsItem;
        private ShopItem _crystalPackItem;
        private int _loadVersion;
        private bool _offersRecorded;
        private RewardedAdRequest _adRequest;
        private RewardedAdService _ads;
        private PurchaseService _iap;
        private Button RestoreButton => _contentRoot.Q<Button>("shop-restore");
        private ShopPurchaseReceipt _pendingPurchase;
        private string _purchaseProfileId;
        private string _purchaseOwnerPlayerId;
        private Button FreeCoinsButton =>
            _contentRoot.Q<Button>("shop-free-coins");
        private Button CrystalPackButton =>
            _contentRoot.Q<Button>("shop-crystal-pack");
        private Label PurchaseMessage =>
            _contentRoot.Q<Label>("shop-purchase-message");

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.ShopScreen;

        public ShopScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        protected override string ScreenBackgroundAddress => "ShopScreenBackgroundSprite";

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return ScreenLayout.Fit(
                content.Q<VisualElement>("shop-viewport"),
                content.Q<VisualElement>("shop-scale-frame"),
                content.Q<VisualElement>("shop-design"),
                new Vector2(DesignWidth, DesignHeight), stretchWidth: true);
        }

        protected override void BindView()
        {
            // Карточки сохраняют размеры, пока загружается каталог предложений.
            _loadVersion++;
            _offersRecorded = false;
            _freeCoinsItem = null;
            _crystalPackItem = null;
            ApplyOfferState();
        }

        protected override async Task LoadDataAsync()
        {
            int loadVersion = _loadVersion;
            var shopItems = await ShopManager.GetShopItems();
            if (loadVersion != _loadVersion)
                return;

            // Связываем утверждённые карточки с каталогом по смыслу сделки.
            _freeCoinsItem = shopItems?.FirstOrDefault(
                item =>
                    item.resource == ResourceType.Advertisement &&
                    item.type == ResourceType.Coins);
            _crystalPackItem = shopItems?.FirstOrDefault(
                item =>
                    item.resource == ResourceType.Coins &&
                    item.type == ResourceType.Crystals);

            ApplyOfferState();
        }

        protected override void OnSubscribeToEvents()
        {
            // Подключаем предложения и обновления баланса.
            FreeCoinsButton?.RegisterCallback<ClickEvent>(
                OnFreeCoinsClicked);
            CrystalPackButton?.RegisterCallback<ClickEvent>(
                OnCrystalPackClicked);
            ResourceManager.BalanceChanged += OnBalanceChanged;
            foreach (var name in new[] { "iap-noads", "iap-crystals10", "iap-crystals30", "iap-starter" })
                _contentRoot.Q<Button>(name)?.RegisterCallback<ClickEvent>(OnIapClicked);
            RestoreButton?.RegisterCallback<ClickEvent>(OnRestoreClicked);
            _iap = PurchaseService.Instance;
            _iap.Changed += OnIapChanged;
            _ = _iap.ConnectAsync();
            _ads = RewardedAdService.Instance;
            _ads.Changed += OnAdvertisementChanged;
            OnAdvertisementChanged();
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _loadVersion++;
            FreeCoinsButton?.UnregisterCallback<ClickEvent>(
                OnFreeCoinsClicked);
            CrystalPackButton?.UnregisterCallback<ClickEvent>(
                OnCrystalPackClicked);
            ResourceManager.BalanceChanged -= OnBalanceChanged;
            foreach (var name in new[] { "iap-noads", "iap-crystals10", "iap-crystals30", "iap-starter" })
                _contentRoot.Q<Button>(name)?.UnregisterCallback<ClickEvent>(OnIapClicked);
            RestoreButton?.UnregisterCallback<ClickEvent>(OnRestoreClicked);
            if (_iap != null) _iap.Changed -= OnIapChanged;
            if (_ads != null)
            {
                _ads.Changed -= OnAdvertisementChanged;
                _ads.CancelContext(_adRequest);
                _adRequest = null;
            }
        }

        private void OnFreeCoinsClicked(ClickEvent _)
        {
            HidePurchaseMessage();
            Buy(_freeCoinsItem);
        }

        private void OnCrystalPackClicked(ClickEvent _)
        {
            // Проверяем доступность каталогового предложения и баланса.
            if (_crystalPackItem == null)
            {
                return;
            }

            if (!ShopManager.CanBuyItem(_crystalPackItem))
            {
                ShowPurchaseMessage("shop_insufficient_coins_for_crystals");
                return;
            }

            // Убираем прежнее сообщение и выполняем покупку.
            HidePurchaseMessage();
            Buy(_crystalPackItem);
        }

        private void Buy(ShopItem item)
        {
            if (item == null || !ShopManager.CanBuyItem(item))
            {
                return;
            }

            string requestId = item.resource == ResourceType.Advertisement ? null : GetPurchaseRequestId(item);
            if (!ShopManager.BuyItem(item, requestId))
                ShowPurchaseMessage("shop_save_failed");
            else if (item.resource == ResourceType.Advertisement)
            {
                _adRequest = RewardedAdService.Instance.ActiveRequest;
                OnAdvertisementChanged();
            }
            else
                _pendingPurchase = null;
            ApplyOfferState();
        }

        /// <summary>Повторяет отказ записи с тем же ID только для того же профиля и предложения.</summary>
        private string GetPurchaseRequestId(ShopItem item)
        {
            if (_pendingPurchase == null || _purchaseProfileId != GameDataManager.ProfileId ||
                _purchaseOwnerPlayerId != GameDataManager.OwnerPlayerId || !_pendingPurchase.Matches(item))
            {
                _pendingPurchase = ShopPurchaseReceipt.Capture(Guid.NewGuid().ToString("N"), item);
                _purchaseProfileId = GameDataManager.ProfileId;
                _purchaseOwnerPlayerId = GameDataManager.OwnerPlayerId;
            }
            return _pendingPurchase.RequestId;
        }

        private void OnBalanceChanged(ResourceType resourceType, int _)
        {
            if (resourceType == ResourceType.Coins)
            {
                // Обновляем доступность предложений после изменения монет.
                ApplyOfferState();

                // Скрываем предупреждение после пополнения достаточного баланса.
                if (_crystalPackItem != null &&
                    ShopManager.CanBuyItem(_crystalPackItem))
                {
                    HidePurchaseMessage();
                }
            }
        }

        private void ApplyOfferState()
        {
            if (!_offersRecorded && _freeCoinsItem != null && _contentRoot.panel != null)
            {
                _offersRecorded = true;
                MonetizationEvent.Record("offer", "shop", amount: _freeCoinsItem.amount);
                MonetizationEvent.Record("offer", PurchaseCatalog.NoAds);
                MonetizationEvent.Record("offer", PurchaseCatalog.Crystals10);
                MonetizationEvent.Record("offer", PurchaseCatalog.Crystals30);
                if (StarterPackEligibility.IsVisible) MonetizationEvent.Record("offer", PurchaseCatalog.Starter);
            }
            FreeCoinsButton?.SetEnabled(_freeCoinsItem != null && ShopManager.CanBuyItem(_freeCoinsItem));
            CrystalPackButton?.SetEnabled(_crystalPackItem != null && ShopManager.CanBuyItem(_crystalPackItem));
            var coinLabel = _contentRoot.Q<Label>("shop-free-amount");
            var gemLabel = _contentRoot.Q<Label>("shop-exchange-amount");
            if (coinLabel != null) coinLabel.text = Format("shop_coin_amount", _freeCoinsItem?.amount ?? 50);
            if (gemLabel != null) gemLabel.text = Format("shop_crystal_amount", _crystalPackItem?.amount ?? 1);
            if (CrystalPackButton != null) CrystalPackButton.text = Format("shop_coin_amount", _crystalPackItem?.price ?? 500);
            if (FreeCoinsButton != null)
            {
                var remaining = _ads?.ShopCooldownRemaining ?? TimeSpan.Zero;
                FreeCoinsButton.text = remaining > TimeSpan.Zero
                    ? LocalizationManager.GetLocalizedString("shop_cooldown").Replace("{0}",
                        $"{(int)Math.Ceiling(remaining.TotalSeconds) / 60:00}:{(int)Math.Ceiling(remaining.TotalSeconds) % 60:00}")
                    : LocalizationManager.GetLocalizedString(_adRequest != null && !_adRequest.IsFinished ? _ads.StatusKey : "win_bonus_video");
            }
            bool starter = StarterPackEligibility.IsVisible;
            _contentRoot.Q("shop-onetime-row")?.EnableInClassList("shop-split", starter);
            SetIapButton("iap-noads", PurchaseCatalog.NoAds);
            SetIapButton("iap-crystals10", PurchaseCatalog.Crystals10);
            SetIapButton("iap-crystals30", PurchaseCatalog.Crystals30);
            SetIapButton("iap-starter", PurchaseCatalog.Starter);
            RestoreButton?.SetEnabled(_iap?.CanRestore == true);
        }

        private static string Format(string key, int value) => LocalizationManager.GetLocalizedString(key).Replace("{0}", value.ToString());
        private void SetIapButton(string name, string sku)
        {
            var button = _contentRoot.Q<Button>(name);
            if (button == null) return;
            button.SetEnabled(_iap?.CanBuy(sku) == true);
            button.text = _iap?.IsOwned(sku) == true ? LocalizationManager.GetLocalizedString("iap_owned") :
                _iap?.LocalizedPrice(sku) ?? LocalizationManager.GetLocalizedString("iap_unavailable");
        }
        private void OnIapClicked(ClickEvent evt)
        {
            string sku = (evt.currentTarget as Button)?.name switch
            {
                "iap-noads" => PurchaseCatalog.NoAds, "iap-crystals10" => PurchaseCatalog.Crystals10,
                "iap-crystals30" => PurchaseCatalog.Crystals30, "iap-starter" => PurchaseCatalog.Starter, _ => null
            };
            if (sku != null) _iap?.Buy(sku);
        }
        private void OnRestoreClicked(ClickEvent evt) => _iap?.Restore();
        private void OnIapChanged()
        {
            ApplyOfferState();
            if (!string.IsNullOrEmpty(_iap?.StatusKey) && _iap.StatusKey != "iap_unavailable") ShowPurchaseMessage(_iap.StatusKey);
        }

        private void OnAdvertisementChanged()
        {
            ApplyOfferState();
            string key = _ads?.StatusKey;

            // Успех относится только к рекламе текущего посещения магазина.
            bool isPreviousReward = key == "ads_reward_granted" &&
                _adRequest?.State != RewardedAdState.Completed;
            if (string.IsNullOrEmpty(key) || isPreviousReward)
                HidePurchaseMessage();
            else
                ShowPurchaseMessage(key);
        }

        private void ShowPurchaseMessage(string key)
        {
            if (PurchaseMessage != null)
            {
                if (PurchaseMessage is LocalizedLabel localized)
                    localized.key = key;
                PurchaseMessage.text = LocalizationManager.GetLocalizedString(key);
                PurchaseMessage.style.display = DisplayStyle.Flex;
            }
        }

        private void HidePurchaseMessage()
        {
            if (PurchaseMessage != null)
            {
                PurchaseMessage.style.display = DisplayStyle.None;
            }
        }
    }
}
