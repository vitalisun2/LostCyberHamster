using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class SelectLevelScreenController : ScreenController
    {
        private enum SelectionState
        {
            DayPart,
            Levels
        }

        private const int _levelsPerPage = 4;

        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");
        private Button _buttonBack => _contentRoot.Q<Button>("btn_back");

        private VisualElement _levelsContainer => _contentRoot.Q<VisualElement>("levels_container");
        private VisualElement _locationImage => _contentRoot.Q<VisualElement>("location__image");
        private Label _locationTitle => _contentRoot.Q<Label>("location__title");

        private readonly List<Action> _onClickedLevelSubscribe = new();
        private readonly List<Action> _onClickedLevelUnsubscribe = new();

        private Button _buttonNextLocation => _contentRoot.Q<Button>("btn__location-next");
        private Button _buttonPrevLocation => _contentRoot.Q<Button>("btn__location-prev");

        private bool _isNotOpenedLocationShown;
        private int _currentLocationIndex = LevelManager.GetLocationIndex();

        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;

        private LevelSelectionModel _selectionModel;
        private LocationView _selectedLocationView;
        private PartView _selectedPartView;
        private SelectionState _state = SelectionState.DayPart;
        private int _currentLevelPage;
        private int _openedLocationCount;

        public SelectLevelScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override ScreenEnum _screenAssetName => ScreenEnum.SelectLevelScreen;

        protected override async Task OnLoadAsync()
        {
            InitializeSelectionModel();
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            await Init();
        }

        private void InitializeSelectionModel()
        {
            _selectionModel = LevelSelectionModel.Create();
            _openedLocationCount = _selectionModel.Locations.Count(
                location => location.IsUnlocked);
            _state = SelectionState.DayPart;
            _selectedPartView = null;
            _currentLevelPage = 0;
        }

        private LocationView GetCurrentLocationView()
        {
            if (_selectionModel?.Locations == null || _selectionModel.Locations.Count == 0)
            {
                return null;
            }

            if (_currentLocationIndex < 0)
            {
                _currentLocationIndex = 0;
            }

            if (_currentLocationIndex >= _selectionModel.Locations.Count)
            {
                _currentLocationIndex = _selectionModel.Locations.Count - 1;
            }

            return _selectionModel.Locations[_currentLocationIndex];
        }

        private async Task Init()
        {
            await InitLocation();

            _onClickedLevelSubscribe.Clear();
            OnUnsubscribeFromEvents();
            _onClickedLevelUnsubscribe.Clear();
            _levelsContainer.Clear();

            if (_state == SelectionState.Levels && _selectedPartView != null)
            {
                PopulateLevelCards(_selectedPartView);
                UpdateTitle(ResolveLocalizedTitle(
                    _selectedPartView.Key,
                    _selectedPartView.DisplayName));
                UpdateBackButton(true);
                UpdateLevelPagingButtons(_selectedPartView);
            }
            else
            {
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                var locationView = GetCurrentLocationView();
                PopulateDayCards(locationView);
                UpdateTitle(ResolveLocalizedTitle(
                    locationView?.Key,
                    locationView?.DisplayName));
                UpdateBackButton(false);
                ResetLocationButtons();
            }

            OnSubscribeToEvents();
            await Apear();
        }

        private async Task InitLocation()
        {
            _currentLocationIndex = _openedLocationCount > 0
                ? Mathf.Clamp(_currentLocationIndex, 0, _openedLocationCount - 1)
                : 0;
            var locationView = GetCurrentLocationView();
            Sprite resolvedSprite = null;

            if (locationView != null)
            {
                _selectedLocationView = locationView;

                if (!string.IsNullOrWhiteSpace(locationView.ImageAddress))
                {
                    resolvedSprite = await TryLoadSprite(locationView.ImageAddress);
                }
            }

            if (resolvedSprite == null)
            {
                var locationInfos = LevelManager.LocationInfoList?.locations
                                    ?? Array.Empty<Assets.Scripts.Common.Models.LocationInfo>();
                if (locationView != null &&
                    locationView.Index >= 0 &&
                    locationView.Index < locationInfos.Length)
                {
                    var fallbackInfo = locationInfos[locationView.Index];
                    if (!string.IsNullOrWhiteSpace(fallbackInfo?.image))
                    {
                        resolvedSprite = await TryLoadSprite(fallbackInfo.image);
                    }
                }
            }

            if (resolvedSprite != null)
            {
                _locationImage.style.backgroundImage = new StyleBackground(resolvedSprite.texture);
            }
        }

        private void PopulateDayCards(LocationView locationView)
        {
            var parts = locationView?.Parts ?? Array.Empty<PartView>();
            if (parts.Count == 0)
            {
                var emptyLabel = new Label("No parts configured for this location.")
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        alignSelf = Align.Center
                    }
                };
                _levelsContainer.Add(emptyLabel);
                return;
            }

            foreach (var partView in parts)
            {
                var primaryLevel = partView.Levels.FirstOrDefault();
                var primaryLevelKey = primaryLevel.Address?.Trim();
                var levelItem = new LevelItem()
                {
                    style = { opacity = 0f }
                };
                levelItem.ConfigureForPart(
                    partView.Key,
                    partView.DisplayName,
                    string.Empty,
                    primaryLevelKey ?? string.Empty,
                    partView.IsUnlocked,
                    primaryLevel.Stars);

                _levelsContainer.Add(levelItem);

                if (!levelItem.IsLocked)
                {
                    var locationCapture = locationView;
                    var partCapture = partView;
                    _onClickedLevelSubscribe.Add(() =>
                    {
                        levelItem.RegisterCallback<ClickEvent>(evt => OnClickDayPart(evt, locationCapture, partCapture));
                    });
                    _onClickedLevelUnsubscribe.Add(() =>
                    {
                        levelItem.UnregisterCallback<ClickEvent>(evt => OnClickDayPart(evt, locationCapture, partCapture));
                    });
                }
            }
        }


        private void PopulateLevelCards(PartView partView)
        {
            var levels = partView.Levels ?? Array.Empty<LevelProgress>();
            if (levels.Count == 0)
            {
                var emptyLabel = new Label("No levels configured for this part of day.")
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        alignSelf = Align.Center
                    }
                };
                _levelsContainer.Add(emptyLabel);
                return;
            }

            var maxPage = Mathf.Max(0, (levels.Count - 1) / _levelsPerPage);
            _currentLevelPage = Mathf.Clamp(_currentLevelPage, 0, maxPage);
            var startIndex = _currentLevelPage * _levelsPerPage;
            var pageLevels = levels.Skip(startIndex).Take(_levelsPerPage).ToList();

            if (pageLevels.Count == 0)
            {
                _currentLevelPage = 0;
                pageLevels = levels.Take(_levelsPerPage).ToList();
                startIndex = 0;
            }

            for (int i = 0; i < pageLevels.Count; i++)
            {
                var levelRef = pageLevels[i];
                var displayIndex = startIndex + i + 1;
                var levelItem = new LevelItem();
                levelItem.ConfigureForLevel(levelRef, displayIndex, partView.Key, string.Empty);
                var canonicalLevelKey = string.IsNullOrWhiteSpace(levelRef.Address)
                    ? levelRef.LevelKey
                    : levelRef.Address.Trim();
                levelItem.style.opacity = 0f;
                _levelsContainer.Add(levelItem);

                if (!levelItem.IsLocked)
                {
                    var keyCapture = canonicalLevelKey;
                    _onClickedLevelSubscribe.Add(() =>
                    {
                        levelItem.RegisterCallback<ClickEvent>(evt => OnClickLevel(evt, keyCapture));
                    });
                    _onClickedLevelUnsubscribe.Add(() =>
                    {
                        levelItem.UnregisterCallback<ClickEvent>(evt => OnClickLevel(evt, keyCapture));
                    });
                }
            }
        }
        private async Task ShowNotOpenedLocation()
        {
            _levelsContainer.Clear();
            var notOpenedLocationImage = await Addressables.LoadAssetAsync<Sprite>("not_opened_preview").Task;
            if (notOpenedLocationImage != null)
            {
                _locationImage.style.backgroundImage = new StyleBackground(notOpenedLocationImage.texture);
            }

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
                        await Task.Delay(10);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private async void OnClickDayPart(ClickEvent evt, LocationView locationView, PartView partView)
        {
            evt.StopPropagation();
            _selectedLocationView = locationView;
            _selectedPartView = partView;
            _state = SelectionState.Levels;
            _currentLevelPage = 0;
            await Init();
        }

        private void OnClickLevel(ClickEvent evt, string levelName)
        {
            LevelController.Instance.SetCurrentLevel(levelName);
            SceneManager.LoadScene("Game");
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }

        private async void OnClickPrevLocation(ClickEvent evt)
        {
            if (_state == SelectionState.Levels && TryChangeLevelPage(-1))
            {
                await Init();
                return;
            }

            var totalLocations = _selectionModel?.Locations.Count ?? 0;

            if (_openedLocationCount > 0 && _openedLocationCount == totalLocations)
            {
                _currentLocationIndex = (_currentLocationIndex - 1 + _openedLocationCount) % _openedLocationCount;
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                await Init();
                return;
            }

            if (_isNotOpenedLocationShown)
            {
                _currentLocationIndex = _openedLocationCount > 0 ? _openedLocationCount - 1 : 0;
                _isNotOpenedLocationShown = false;
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                await Init();
                return;
            }

            _currentLocationIndex--;

            if (_currentLocationIndex < 0)
            {
                await ShowNotOpenedLocation();
                _isNotOpenedLocationShown = true;
                _currentLocationIndex = 0;
                return;
            }

            _state = SelectionState.DayPart;
            _selectedPartView = null;
            await Init();
        }

        private async void OnClickNextLocation(ClickEvent evt)
        {
            if (_state == SelectionState.Levels && TryChangeLevelPage(1))
            {
                await Init();
                return;
            }

            var totalLocations = _selectionModel?.Locations.Count ?? 0;

            if (_openedLocationCount > 0 && _openedLocationCount == totalLocations)
            {
                _currentLocationIndex = (_currentLocationIndex + 1) % _openedLocationCount;
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                await Init();
                return;
            }

            if (_isNotOpenedLocationShown)
            {
                _currentLocationIndex = 0;
                _isNotOpenedLocationShown = false;
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                await Init();
                return;
            }

            _currentLocationIndex++;

            if (_currentLocationIndex >= _openedLocationCount)
            {
                await ShowNotOpenedLocation();
                _isNotOpenedLocationShown = true;
                _currentLocationIndex = Mathf.Clamp(_openedLocationCount - 1, 0, int.MaxValue);
                return;
            }

            _state = SelectionState.DayPart;
            _selectedPartView = null;
            await Init();
        }

        private static async Task<Sprite> TryLoadSprite(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            try
            {
                return await Addressables.LoadAssetAsync<Sprite>(address).Task;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SelectLevelScreen] Failed to load sprite '{address}': {ex.Message}");
                return null;
            }
        }

        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        private void OnClickBtnBack(ClickEvent evt)
        {
            _state = SelectionState.DayPart;
            _selectedPartView = null;
            _currentLevelPage = 0;
            _ = Init();
        }

        private void OnClickBtnSettings(ClickEvent evt)
        {
            SettingsScreenController.OpenFrom(ScreenEnum.SelectLevelScreen);
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonBack?.RegisterCallback<ClickEvent>(OnClickBtnBack);
            _buttonNextLocation?.RegisterCallback<ClickEvent>(OnClickNextLocation);
            _buttonPrevLocation?.RegisterCallback<ClickEvent>(OnClickPrevLocation);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickBtnAddMoney);

            foreach (var subscribeAction in _onClickedLevelSubscribe)
            {
                subscribeAction.Invoke();
            }
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickBtnSettings);
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickBtnHome);
            _buttonBack?.UnregisterCallback<ClickEvent>(OnClickBtnBack);
            _buttonNextLocation?.UnregisterCallback<ClickEvent>(OnClickNextLocation);
            _buttonPrevLocation?.UnregisterCallback<ClickEvent>(OnClickPrevLocation);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickBtnAddMoney);

            foreach (var unsubscribeAction in _onClickedLevelUnsubscribe)
            {
                unsubscribeAction.Invoke();
            }
        }

        private void UpdateTitle(string text)
        {
            if (_locationTitle != null)
            {
                _locationTitle.text = text;
            }
        }

        /// <summary>
        /// Возвращает локализованный заголовок экрана с резервным отображаемым именем.
        /// </summary>
        private static string ResolveLocalizedTitle(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            var localized = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(localized) ||
                   string.Equals(localized, key, StringComparison.Ordinal)
                ? fallback ?? key
                : localized;
        }

        private void UpdateBackButton(bool visible)
        {
            if (_buttonBack == null)
            {
                return;
            }

            _buttonBack.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ResetLocationButtons()
        {
            _buttonPrevLocation?.SetEnabled(true);
            _buttonNextLocation?.SetEnabled(true);
        }

        private void UpdateLevelPagingButtons(PartView partView)
        {
            var levels = partView.Levels ?? Array.Empty<LevelProgress>();
            var maxPage = Mathf.Max(0, (levels.Count - 1) / _levelsPerPage);
            _buttonPrevLocation?.SetEnabled(_currentLevelPage > 0);
            _buttonNextLocation?.SetEnabled(_currentLevelPage < maxPage);
        }

        private bool TryChangeLevelPage(int delta)
        {
            if (_selectedPartView == null)
            {
                return false;
            }

            var levels = _selectedPartView.Levels ?? Array.Empty<LevelProgress>();
            if (levels.Count <= _levelsPerPage)
            {
                return false;
            }

            var maxPage = Mathf.Max(0, (levels.Count - 1) / _levelsPerPage);
            var newPage = Mathf.Clamp(_currentLevelPage + delta, 0, maxPage);
            if (newPage == _currentLevelPage)
            {
                return false;
            }

            _currentLevelPage = newPage;
            return true;
        }
    }
}
