using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class ShopModalController : ModalController
    {
        private VisualTreeAsset _shopItemElement;

        protected override ScreenEnum _modalAssetName => ScreenEnum.ShopModal;

        private VisualElement _shopModalContent => _modalContent.Q<VisualElement>("shop-modal__content");

        private List<Action> _actionsRegister = new List<Action>();
        private List<Action> _actionsUnregister = new List<Action>();


        public ShopModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override async Task OnShowAsync()
        {
            var op = Addressables.LoadAssetAsync<VisualTreeAsset>("ShopItem");
            op.WaitForCompletion();
            _shopItemElement = op.Result;

            var shopItems = await ShopManager.GetShopItems();
            _shopModalContent.Clear();
            _actionsRegister.Clear();
            _actionsUnregister.Clear();
            foreach (var item in shopItems)
            {
                InitShop(item);
            }
            OnSubscribeToEvents();
        }

        private void InitShop(ShopItem item)
        {
            var op = Addressables.LoadAssetAsync<Texture2D>(item.imageAddress);
            op.WaitForCompletion();
            var itemImage = op.Result;
            Addressables.Release(op);
            var ve = new VisualElement();
            ve.name = item.name;
            _shopItemElement.CloneTree(ve);

            var button = ve.Q<Button>("shop-item__btn-get");
            var localizationKey = item.price > 0 ? "btn_buy" : "btn_get";
            button.text = LocalizationManager.GetLocalizedString(localizationKey);

            ve.Q<VisualElement>("shop-item__image").style.backgroundImage = itemImage;
            if (item.price > 0)
            {
                ve.Q<Label>("shop-item__price-label").text = item.price.ToString();
                if (!ShopManager.CanBuyItem(item))
                {
                    button.SetEnabled(false);
                }
            }
            else
            {
                ve.Q<Label>("shop-item__price-label").style.display = DisplayStyle.None;
            }

            ve.Q<VisualElement>("shop-item__price-image").style.backgroundImage = ResourceUIHelper.GetResourceImage(item.resource);

            _actionsRegister.Add(() => button.RegisterCallback<ClickEvent>(evt => BuyItem(item, button)));
            _actionsUnregister.Add(() => button.UnregisterCallback<ClickEvent>(evt => BuyItem(item, button)));

            _shopModalContent.Add(ve);
        }

        private void BuyItem(ShopItem item, Button button)
        {
            button.SetEnabled(false);
            GameEventsManager.OnItemBought += (id, type, amount) => OnBoughtAction(button);

            ShopManager.BuyItem(item);
        }

        protected void OnBoughtAction(Button button)
        {
            button.SetEnabled(true);
            GameEventsManager.OnItemBought -= (id, type, amount) => OnBoughtAction(button);
            UIManager.OnRepaintScreen();
            UIManager.OnRepaintModal();
        }

        protected override void OnSubscribeToEvents()
        {
            foreach (var action in _actionsRegister)
            {
                action.Invoke();
            }
        }


        protected override void OnUnsubscribeFromEvents()
        {
            foreach (var action in _actionsUnregister)
            {
                action.Invoke();
            }
        }

    }
}

/*

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.AddressableAssets;

public class ShopController : MonoBehaviour
{
    public VisualElement shopContent;  // Reference to the UI element to place shop items
    public VisualTreeAsset shopItemTemplate;  // Template for each shop item UI
    public AssetReference shopItemsJson;  // Addressable reference to the shop items JSON file

    public Label shopNameLabel;   // Label in the UI for the shop name (optional)
    public Label shopDescriptionLabel;  // Label in the UI for the shop description (optional)

    private void Start()
    {
        // Initialize the ShopManager and load shop items from JSON
        ShopManager.Initialize(shopItemsJson, DrawShopItems);
    }

    // Draw each shop item in the UI
    private void DrawShopItems()
    {
        // Optional: Set the shop name and description if you want to display them
        if (shopNameLabel != null) shopNameLabel.text = ShopManager.GetShopName();
        if (shopDescriptionLabel != null) shopDescriptionLabel.text = ShopManager.GetShopDescription();

        var shopItems = ShopManager.GetShopItems();

        foreach (ShopItem item in shopItems)
        {
            VisualElement shopItemElement = shopItemTemplate.Instantiate();
            shopItemElement.Q<Label>("itemName").text = item.name;

            // Determine the price label (Coins, Crystals, or Ad)
            string priceText = item.resource == ResourceType.Ad
                ? "Watch Ad"
                : item.price.ToString() + " " + item.resource.ToString();

            shopItemElement.Q<Label>("itemPrice").text = priceText;

            // Load the image via Addressables (assume there's a placeholder for itemImage)
            LoadItemImage(item.imageAddress, shopItemElement);

            // Set up the button click for buying or watching an ad
            shopItemElement.Q<Button>("purchaseButton").clicked += () =>
            {
                if (item.resource == ResourceType.Ad)
                {
                    // Handle ad-based rewards
                    WatchAdForItem(item);
                }
                else
                {
                    // Try to buy the item
                    bool success = ShopManager.BuyItem(item);
                    if (success)
                    {
                        Debug.Log($"Purchased {item.name}.");
                    }
                }
            };

            // Add the item to the content area in the UI
            shopContent.Add(shopItemElement);
        }
    }

    // Load item image from Addressables
    private void LoadItemImage(string imageAddress, VisualElement shopItemElement)
    {
        Addressables.LoadAssetAsync<Sprite>(imageAddress).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var sprite = handle.Result;
                var image = new UnityEngine.UIElements.Image();
                image.sprite = sprite;
                shopItemElement.Q("itemImage").Add(image); // Assuming there's a placeholder named "itemImage"
            }
            else
            {
                Debug.LogError("Failed to load item image.");
            }
        };
    }

    // Simulate watching an ad and rewarding coins
    private void WatchAdForItem(ShopItem item)
    {
        Debug.Log($"Watching ad for {item.name}...");
        // Simulate ad-watching completion
        ShopManager.RewardCoinsFromAd(item);
    }
}
*/