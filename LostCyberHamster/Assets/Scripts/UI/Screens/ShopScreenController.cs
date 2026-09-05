using System;
using System.Linq;
using System.Threading.Tasks;
using GameAds;
using GameManagement;
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
        private RewardedAdRequest _adRequest;
        private RewardedAdService _ads;
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
                new Vector2(DesignWidth, DesignHeight));
        }

        protected override void BindView()
        {
            // Карточки сохраняют размеры, пока загружается каталог предложений.
            _loadVersion++;
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
            FreeCoinsButton?.SetEnabled(_freeCoinsItem != null && ShopManager.CanBuyItem(_freeCoinsItem));
            CrystalPackButton?.SetEnabled(_crystalPackItem != null);
        }

        private void OnAdvertisementChanged()
        {
            ApplyOfferState();
            string key = _ads?.StatusKey;
            if (string.IsNullOrEmpty(key))
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
