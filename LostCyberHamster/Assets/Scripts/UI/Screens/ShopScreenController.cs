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

        private VisualElement Viewport =>
            _contentRoot.Q<VisualElement>("shop-viewport");
        private VisualElement ScaleFrame =>
            _contentRoot.Q<VisualElement>("shop-scale-frame");
        private VisualElement Design =>
            _contentRoot.Q<VisualElement>("shop-design");
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

        protected override async Task OnLoadAsync()
        {
            // Фон магазина заполняет внешний полноэкранный контейнер.
            await ChangeBackgroundAsync(
                "ShopScreenBackgroundSprite",
                ScaleMode.ScaleAndCrop);

            var shopItems = await ShopManager.GetShopItems();

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
            // Подключаем предложения, баланс и адаптивный design frame.
            FreeCoinsButton?.RegisterCallback<ClickEvent>(
                OnFreeCoinsClicked);
            CrystalPackButton?.RegisterCallback<ClickEvent>(
                OnCrystalPackClicked);
            ResourceManager.BalanceChanged += OnBalanceChanged;
            Viewport?.RegisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
            Viewport?.schedule.Execute(
                () => ApplyResponsiveLayout(Viewport.contentRect.size));
        }

        protected override void OnUnsubscribeFromEvents()
        {
            FreeCoinsButton?.UnregisterCallback<ClickEvent>(
                OnFreeCoinsClicked);
            CrystalPackButton?.UnregisterCallback<ClickEvent>(
                OnCrystalPackClicked);
            ResourceManager.BalanceChanged -= OnBalanceChanged;
            Viewport?.UnregisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
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

        private void OnViewportGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveLayout(evt.newRect.size);
        }

        private void ApplyResponsiveLayout(Vector2 viewportSize)
        {
            float width = Mathf.Max(1f, viewportSize.x);
            float height = Mathf.Max(1f, viewportSize.y);
            float scale = Mathf.Min(
                width / DesignWidth,
                height / DesignHeight);

            ScaleFrame.style.width = DesignWidth * scale;
            ScaleFrame.style.height = DesignHeight * scale;
            Design.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }
    }
}
