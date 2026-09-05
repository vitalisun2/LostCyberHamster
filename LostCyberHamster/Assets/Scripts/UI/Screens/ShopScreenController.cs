using System.Linq;
using System.Threading.Tasks;
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
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _loadVersion++;
            FreeCoinsButton?.UnregisterCallback<ClickEvent>(
                OnFreeCoinsClicked);
            CrystalPackButton?.UnregisterCallback<ClickEvent>(
                OnCrystalPackClicked);
            ResourceManager.BalanceChanged -= OnBalanceChanged;
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
                ShowPurchaseMessage();
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

            ShopManager.BuyItem(item);
            ApplyOfferState();
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
            FreeCoinsButton?.SetEnabled(_freeCoinsItem != null);
            CrystalPackButton?.SetEnabled(_crystalPackItem != null);
        }

        private void ShowPurchaseMessage()
        {
            if (PurchaseMessage != null)
            {
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
