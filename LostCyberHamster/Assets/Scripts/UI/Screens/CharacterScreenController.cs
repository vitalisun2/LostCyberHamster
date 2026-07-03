using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class CharacterScreenController : ScreenController
    {
        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");

        private VisualElement _skinImage => _contentRoot.Q<VisualElement>("skin-image");
        private Button _buttonChangeSkin => _contentRoot.Q<Button>("skin-btn-change");
        private Button _buttonNextSkin => _contentRoot.Q<Button>("btn-skin-next");
        private Button _buttonPrevSkin => _contentRoot.Q<Button>("btn-skin-prev");

        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;

        private Label _labelPutOn => _contentRoot.Q<Label>("skin--put-on");


        private int _currentSkinIndex = -1;

        private Label _labelSkinName => _contentRoot.Q<Label>("skin-name");
        public CharacterScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }


        protected override ScreenEnum _screenAssetName => ScreenEnum.CharacterScreen;

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            if (_currentSkinIndex < 0)
            {
                _currentSkinIndex = SkinManager.AvailableSkins.IndexOf(SkinManager.CurrentSkin);
            }

            await ShowSkinAsync(_currentSkinIndex);
        }

        private async Task ShowSkinAsync(int index)
        {
            var skin = SkinManager.AvailableSkins.ElementAt(index);
            _labelSkinName.text = skin?.Name;
            _skinImage.style.backgroundImage = skin?.HamsterSprite.texture;
            _buttonChangeSkin.style.display = DisplayStyle.Flex;
            _labelPutOn.style.display = DisplayStyle.None;


            if (skin.IsPurchased)
            {
                _buttonChangeSkin.text = LocalizationManager.GetLocalizedString("put-on");
                _buttonChangeSkin.iconImage = null;
                _buttonChangeSkin.SetEnabled(true);
                if (SkinManager.CurrentSkin == skin)
                {
                    _buttonChangeSkin.style.display = DisplayStyle.None;
                    _labelPutOn.style.display = DisplayStyle.Flex;
                    _labelPutOn.text = LocalizationManager.GetLocalizedString("put-on");
                }
                return;
            }
            var icon = await GetResourceIconAsync(skin?.PriceType);
            if (icon is not null)
            {
                _buttonChangeSkin.iconImage = Background.FromTexture2D(icon);
            }
            _buttonChangeSkin.text = " " + skin?.Price.ToString();

            if (!SkinManager.CanPurchaseSkin(skin.Id))
            {
                _buttonChangeSkin.SetEnabled(false);
            }
        }

        private async Task<Texture2D> GetResourceIconAsync(ResourceType? priceType)
        {
            switch (priceType)
            {
                case ResourceType.Crystals:
                    return await Addressables.LoadAssetAsync<Texture2D>("crystal").Task;
                case ResourceType.Coins:
                    return await Addressables.LoadAssetAsync<Texture2D>("coin").Task;
                default:
                    return null;
            }
        }


        protected override void OnSubscribeToEvents()
        {
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonNextSkin?.RegisterCallback<ClickEvent>(OnClickBtnNextSkin);
            _buttonPrevSkin?.RegisterCallback<ClickEvent>(OnClickBtnPrevSkin);
            _buttonChangeSkin?.RegisterCallback<ClickEvent>(OnClickBtnChangeSkin);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
        }

        private async void OnClickBtnChangeSkin(ClickEvent evt)
        {
            var skin = SkinManager.AvailableSkins.ElementAt(_currentSkinIndex);
            if (skin.IsPurchased)
            {
                SkinManager.PutOnSkin(skin.Id);
            }
            else
            {
                SkinManager.PurchaseSkin(skin.Id);
            }

            await ShowSkinAsync(_currentSkinIndex);
        }


        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }


        private void OnClickBtnSettings(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.SettingsModal);
        }

        private async void OnClickBtnNextSkin(ClickEvent evt)
        {
            _currentSkinIndex++;
            if (_currentSkinIndex >= SkinManager.AvailableSkins.Count)
            {
                _currentSkinIndex = 0;
            }
            await ShowSkinAsync(_currentSkinIndex);
        }

        private async void OnClickBtnPrevSkin(ClickEvent evt)
        {
            _currentSkinIndex--;
            if (_currentSkinIndex < 0)
            {
                _currentSkinIndex = SkinManager.AvailableSkins.Count - 1;
            }
            await ShowSkinAsync(_currentSkinIndex);
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }



        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonNextSkin?.UnregisterCallback<ClickEvent>(OnClickBtnNextSkin);
            _buttonPrevSkin?.UnregisterCallback<ClickEvent>(OnClickBtnPrevSkin);
            _buttonChangeSkin?.UnregisterCallback<ClickEvent>(OnClickBtnChangeSkin);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
        }
    }
}
