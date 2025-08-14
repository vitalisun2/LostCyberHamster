using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class SelectLevelScreenController : ScreenController
    {
        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");

        private VisualElement _levelsContainer => _contentRoot.Q<VisualElement>("levels_container");

        private VisualElement _locationImage => _contentRoot.Q<VisualElement>("location__image");

        private VisualElement _locationTitle => _contentRoot.Q<VisualElement>("location__title");

        private List<Action> _onClickedLevelSubscribe = new List<Action>();
        private List<Action> _onClickedLevelUnsubscribe = new List<Action>();

        private Button _buttonNextLocation => _contentRoot.Q<Button>("btn__location-next");

        private Button _buttonPrevLocation => _contentRoot.Q<Button>("btn__location-prev");

        private bool _isNotOpenedLocationShown = false;


        private int _currentLocationIndex = LevelManager.GetLocationIndex();

        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;


        public SelectLevelScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }


        protected override ScreenEnum _screenAssetName => ScreenEnum.SelectLevelScreen;

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            await Init();
        }

        private async Task InitLocation()
        {
            if (_currentLocationIndex >= LevelManager.OpenedLocations.Count)
            {
                _currentLocationIndex = 0;
            }
            var locationInfo = LevelManager.OpenedLocations[_currentLocationIndex];
            var locationImage = await Addressables.LoadAssetAsync<Sprite>(locationInfo.image).Task;
            _locationImage.style.backgroundImage = new StyleBackground(locationImage.texture);
        }


        private async Task Init()
        {
            await InitLocation();
            _onClickedLevelSubscribe.Clear();
            OnUnsubscribeFromEvents();
            _onClickedLevelUnsubscribe.Clear();
            _levelsContainer.Clear();

            foreach (PartOfDayEnum partOfDay in Enum.GetValues(typeof(PartOfDayEnum)))
            {
                var levelItem = new LevelItem(partOfDay, _currentLocationIndex);
                levelItem.style.opacity = 0.0f;
                _levelsContainer.Add(levelItem);
                if (!levelItem.IsLocked)
                {
                    _onClickedLevelSubscribe.Add(() =>
                    {
                        levelItem.RegisterCallback<ClickEvent>(evt => OnClickLevel(evt, LevelManager.GetLevelName(_currentLocationIndex, partOfDay)));
                    });
                    _onClickedLevelUnsubscribe.Add(() =>
                    {
                        levelItem.UnregisterCallback<ClickEvent>(evt => OnClickLevel(evt, LevelManager.GetLevelName(_currentLocationIndex, partOfDay)));
                    });
                }

            }
            OnSubscribeToEvents();
            await Apear();
        }

        private async Task ShowNotOpenedLocation()
        {
            _levelsContainer.Clear();
            var notOpenedLocationImage = await Addressables.LoadAssetAsync<Sprite>("not_opened_preview").Task;
            _locationImage.style.backgroundImage = new StyleBackground(notOpenedLocationImage.texture);

            var starsToOpen = LevelManager.StarsToOpenNewLocation;

            var label = new Label($"You need {starsToOpen} stars to open this location");
            label.style.fontSize = 30;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _levelsContainer.Add(label);
        }

        private async Task Apear()
        {
            try
            {
                foreach (var levelItem in _levelsContainer.Children())
                {
                    for (float i = 0; i < 1; i += 0.1f)
                    {
                        levelItem.style.opacity = i;
                        await Task.Delay(20);
                    }
                }
            }
            catch (Exception)
            {

            }
        }


        protected override void OnSubscribeToEvents()
        {
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonNextLocation?.RegisterCallback<ClickEvent>(OnClickNextLocation);
            _buttonPrevLocation?.RegisterCallback<ClickEvent>(OnClickPrevLocation);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            foreach (var subscribeAction in _onClickedLevelSubscribe)
            {
                subscribeAction.Invoke();
            }
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }


        private async void OnClickPrevLocation(ClickEvent evt)
        {
            // If all locations are opened, loop back to the last location when going backward from the first
            if (LevelManager.OpenedLocations.Count == LevelManager.LocationInfoList.locations.Length)
            {
                _currentLocationIndex = (_currentLocationIndex - 1 + LevelManager.OpenedLocations.Count) % LevelManager.OpenedLocations.Count;
                await Init(); // Loop to the last location if we go back from the first
                return;
            }

            // Check if the "not opened location" message is currently shown
            if (_isNotOpenedLocationShown)
            {
                // Navigate to the last location and reset the flag
                _currentLocationIndex = LevelManager.OpenedLocations.Count - 1;
                _isNotOpenedLocationShown = false;
                await Init(); // Show the last location
                return;
            }

            // Move to the previous location
            _currentLocationIndex--;

            // Check if we've gone past the first opened location
            if (_currentLocationIndex < 0)
            {
                // Show the "not opened location" message and set the flag
                ShowNotOpenedLocation();
                _isNotOpenedLocationShown = true;
                _currentLocationIndex = 0; // Keep the index at the first location
                return;
            }

            // Initialize the previous location as usual
            await Init();
        }


        private async void OnClickNextLocation(ClickEvent evt)
        {
            // If all locations are opened, loop back to the first location
            if (LevelManager.OpenedLocations.Count == LevelManager.LocationInfoList.locations.Length)
            {
                _currentLocationIndex = (_currentLocationIndex + 1) % LevelManager.OpenedLocations.Count;
                await Init(); // Loop to the first location if we exceed the last
                return;
            }

            // If "not opened location" message is currently shown, reset to the first location
            if (_isNotOpenedLocationShown)
            {
                _currentLocationIndex = 0;
                _isNotOpenedLocationShown = false;
                await Init(); // Show the first location
                return;
            }

            // Move to the next location
            _currentLocationIndex++;

            // Check if we've gone past the last opened location
            if (_currentLocationIndex >= LevelManager.OpenedLocations.Count)
            {
                // Show the "not opened location" message and set the flag
                ShowNotOpenedLocation();
                _isNotOpenedLocationShown = true;
                _currentLocationIndex = LevelManager.OpenedLocations.Count - 1; // Keep the index at the last location
                return;
            }

            // Initialize the next location as usual
            await Init();
        }


        private void OnClickLevel(ClickEvent evt, string levelName)
        {
            LevelController.Instance.SetCurrentLevel(levelName);
            SceneManager.LoadScene("Game");
        }


        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }


        private void OnClickBtnSettings(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.SettingsModal);
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonNextLocation?.UnregisterCallback<ClickEvent>(OnClickNextLocation);
            _buttonPrevLocation?.UnregisterCallback<ClickEvent>(OnClickPrevLocation);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            foreach (var unsubscribeAction in _onClickedLevelUnsubscribe)
            {
                unsubscribeAction.Invoke();
            }
        }
    }
}
