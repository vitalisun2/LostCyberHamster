using Unity.Properties;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class SharedHudLane : VisualElement
    {
        public SharedHudLane()
        {
            AddToClassList("shared-hud-lane");
        }
    }

    [UxmlElement]
    public partial class SharedHomeButton : Button
    {
        public SharedHomeButton()
        {
            name = "btn_home";
            tooltip = "Home";
            AddToClassList("shared-hud-control");
            AddToClassList("shared-home-button");
            clicked += OpenHome;
        }

        private static void OpenHome()
        {
            UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
        }
    }

    [UxmlElement]
    public partial class SharedSettingsButton : Button
    {
        private ScreenEnum _originScreen = ScreenEnum.HomeScreen;

        public SharedSettingsButton()
        {
            name = "btn_settings";
            tooltip = "Settings";
            AddToClassList("shared-hud-control");
            AddToClassList("shared-settings-button");
            clicked += OpenSettings;
        }

        public void SetOriginScreen(ScreenEnum originScreen)
        {
            _originScreen = originScreen;
        }

        private void OpenSettings()
        {
            SettingsScreenController.OpenFrom(_originScreen);
        }
    }

    public abstract class SharedCurrencyButton : Button
    {
        private readonly ResourceType _resourceType;
        private readonly Label _balanceLabel;
        private bool _isBalanceSubscribed;

        protected SharedCurrencyButton(
            ResourceType resourceType,
            string iconClass)
        {
            _resourceType = resourceType;
            tooltip = "Shop";
            AddToClassList("shared-hud-control");
            AddToClassList("shared-currency-button");

            var plate = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            plate.AddToClassList("shared-currency-button__plate");

            _balanceLabel = new Label("0")
            {
                pickingMode = PickingMode.Ignore
            };
            _balanceLabel.AddToClassList("shared-currency-button__label");
            plate.Add(_balanceLabel);

            var icon = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("shared-currency-button__icon");
            icon.AddToClassList(iconClass);

            Add(plate);
            Add(icon);

            clicked += OpenShop;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent attachEvent)
        {
            RefreshBalance();
            SubscribeToBalance();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent detachEvent)
        {
            UnsubscribeFromBalance();
        }

        private void SubscribeToBalance()
        {
            if (_isBalanceSubscribed)
            {
                return;
            }

            ResourceManager.BalanceChanged += OnBalanceChanged;
            _isBalanceSubscribed = true;
        }

        private void UnsubscribeFromBalance()
        {
            if (!_isBalanceSubscribed)
            {
                return;
            }

            ResourceManager.BalanceChanged -= OnBalanceChanged;
            _isBalanceSubscribed = false;
        }

        private void RefreshBalance()
        {
            int balance = ResourceManager.IsReady
                ? ResourceManager.GetCurrentBalance(_resourceType)
                : 0;
            SetBalance(balance);
        }

        private void OnBalanceChanged(
            ResourceType resourceType,
            int balance)
        {
            if (resourceType == _resourceType)
            {
                SetBalance(balance);
            }
        }

        private void SetBalance(int balance)
        {
            string value = balance.ToString();
            _balanceLabel.text = value;
            _balanceLabel.EnableInClassList(
                "shared-currency-button__label--compact",
                value.Length >= 8);
        }

        private static void OpenShop()
        {
            UIManager.OnScreenShow?.Invoke(ScreenEnum.ShopScreen);
        }
    }

    [UxmlElement]
    public partial class SharedCoinsButton : SharedCurrencyButton
    {
        public SharedCoinsButton()
            : base(
                ResourceType.Coins,
                "shared-currency-button__icon--coins")
        {
            name = "btn_coins";
        }
    }

    [UxmlElement]
    public partial class SharedGemsButton : SharedCurrencyButton
    {
        public SharedGemsButton()
            : base(
                ResourceType.Crystals,
                "shared-currency-button__icon--gems")
        {
            name = "btn_gems";
        }
    }
}
