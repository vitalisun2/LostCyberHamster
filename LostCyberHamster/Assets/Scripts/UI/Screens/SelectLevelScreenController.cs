using System;
using System.Collections.Generic;
using System.Linq;
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
        private enum SelectionState
        {
            DayPart,
            Levels
        }

        private const int LevelsPerPage = 4;

        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");
        private Button _buttonBack => _contentRoot.Q<Button>("btn_back");

        private VisualElement _levelsContainer => _contentRoot.Q<VisualElement>("levels_container");
        private VisualElement _locationImage => _contentRoot.Q<VisualElement>("location__image");
        private VisualElement _locationTitle => _contentRoot.Q<VisualElement>("location__title");

        private readonly List<Action> _onClickedLevelSubscribe = new();
        private readonly List<Action> _onClickedLevelUnsubscribe = new();

        private Button _buttonNextLocation => _contentRoot.Q<Button>("btn__location-next");
        private Button _buttonPrevLocation => _contentRoot.Q<Button>("btn__location-prev");

        private bool _isNotOpenedLocationShown;
        private int _currentLocationIndex = LevelManager.GetLocationIndex();

        private Button _buttonAddMoney => _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals => _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;

        private LevelSelectionModel _selectionModel;
        private bool _useHierarchicalMode;
        private LevelSelectionModel.LocationView _selectedLocationView;
        private LevelSelectionModel.PartView _selectedPartView;
        private SelectionState _state = SelectionState.DayPart;
        private int _currentLevelPage;

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
            var preferredMode = LevelCatalogService.IsHierarchical
                ? LevelSelectionMode.Hierarchical
                : LevelSelectionMode.Legacy;

            _selectionModel = LevelSelectionModel.Create(preferredMode);
            _useHierarchicalMode = _selectionModel.IsHierarchical;
            _state = SelectionState.DayPart;
            _selectedPartView = null;
            _currentLevelPage = 0;
        }

        private LevelSelectionModel.LocationView GetCurrentLocationView()
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

            if (_useHierarchicalMode && _state == SelectionState.Levels && _selectedPartView != null)
            {
                PopulateLevelCards(_selectedPartView);
                UpdateTitle(_selectedPartView.DisplayName);
                UpdateBackButton(true);
                UpdateLevelPagingButtons(_selectedPartView);
            }
            else
            {
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                var locationView = GetCurrentLocationView();
                PopulateDayCards(locationView);
                UpdateTitle(locationView?.DisplayName ?? string.Empty);
                UpdateBackButton(false);
                ResetLocationButtons();
            }

            OnSubscribeToEvents();
            await Apear();
        }

        private async Task InitLocation()
        {
            var locationView = GetCurrentLocationView();
            if (locationView != null)
            {
                _selectedLocationView = locationView;

                if (!string.IsNullOrWhiteSpace(locationView.ImageAddress))
                {
                    try
                    {
                        var sprite = await Addressables.LoadAssetAsync<Sprite>(locationView.ImageAddress).Task;
                        if (sprite != null)
                        {
                            _locationImage.style.backgroundImage = new StyleBackground(sprite.texture);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SelectLevelScreen] Failed to load location image '{locationView.ImageAddress}': {ex.Message}");
                    }
                }
            }

            if (_currentLocationIndex >= LevelManager.OpenedLocations.Count)
            {
                _currentLocationIndex = 0;
            }

            if (LevelManager.OpenedLocations.Count > 0)
            {
                var locationInfo = LevelManager.OpenedLocations[_currentLocationIndex];
                var locationImage = await Addressables.LoadAssetAsync<Sprite>(locationInfo.image).Task;
                _locationImage.style.backgroundImage = new StyleBackground(locationImage.texture);
            }
        }

        private void PopulateDayCards(LevelSelectionModel.LocationView locationView)
        {
            foreach (PartOfDayEnum partOfDay in Enum.GetValues(typeof(PartOfDayEnum)))
            {
                var partKey = partOfDay.ToString();
                var levelItem = new LevelItem(partOfDay, _currentLocationIndex)
                {
                    style = { opacity = 0f }
                };

                LevelSelectionModel.PartView partView = null;
                if (locationView != null)
                {
                    partView = locationView.Parts.FirstOrDefault(p => string.Equals(p.Key, partKey, StringComparison.OrdinalIgnoreCase));
                    if (partView != null)
                    {
                        var primaryLevelKey = partView.Levels.FirstOrDefault()?.Key ?? levelItem.LevelName;
                        levelItem.ConfigureForPart(partView.Key, partView.DisplayName, string.Empty, primaryLevelKey);
                    }
                }

                _levelsContainer.Add(levelItem);

                if (!_useHierarchicalMode && !levelItem.IsLocked)
                {
                    var levelKey = levelItem.LevelName;
                    _onClickedLevelSubscribe.Add(() =>
                    {
                        levelItem.RegisterCallback<ClickEvent>(evt => OnClickLevel(evt, levelKey));
                    });
                    _onClickedLevelUnsubscribe.Add(() =>
                    {
                        levelItem.UnregisterCallback<ClickEvent>(evt => OnClickLevel(evt, levelKey));
                    });
                }
                else if (_useHierarchicalMode && partView != null && !levelItem.IsLocked)
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

        private void PopulateLevelCards(LevelSelectionModel.PartView partView)
        {
            var levels = partView.Levels ?? Array.Empty<LevelSelectionModel.LevelReference>();
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

            var maxPage = Mathf.Max(0, (levels.Count - 1) / LevelsPerPage);
            _currentLevelPage = Mathf.Clamp(_currentLevelPage, 0, maxPage);
            var startIndex = _currentLevelPage * LevelsPerPage;
            var pageLevels = levels.Skip(startIndex).Take(LevelsPerPage).ToList();

            if (pageLevels.Count == 0)
            {
                _currentLevelPage = 0;
                pageLevels = levels.Take(LevelsPerPage).ToList();
                startIndex = 0;
            }

            for (int i = 0; i < pageLevels.Count; i++)
            {
                var levelRef = pageLevels[i];
                var displayIndex = startIndex + i + 1;
                var levelItem = new LevelItem();
                levelItem.ConfigureForLevel(levelRef, displayIndex, partView.Key, string.Empty);
                levelItem.style.opacity = 0f;
                _levelsContainer.Add(levelItem);

                if (!levelItem.IsLocked)
                {
                    var keyCapture = levelRef.Key;
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

        private async void OnClickDayPart(ClickEvent evt, LevelSelectionModel.LocationView locationView, LevelSelectionModel.PartView partView)
        {
            evt.StopPropagation();
            if (!_useHierarchicalMode)
            {
                return;
            }

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

        private void OnClickLevel(ClickEvent evt, string levelName, bool ignoreLock)
        {
            if (!ignoreLock && !LevelManager.IsLevelOpen(levelName))
            {
                Debug.LogWarning($"Level {levelName} is locked");
                return;
            }

            OnClickLevel(evt, levelName);
        }

        private void OnClickBtnAddMoney(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }

        private async void OnClickPrevLocation(ClickEvent evt)
        {
            if (_useHierarchicalMode && _state == SelectionState.Levels && TryChangeLevelPage(-1))
            {
                await Init();
                return;
            }

            if (LevelManager.OpenedLocations.Count == LevelManager.LocationInfoList.locations.Length)
            {
                _currentLocationIndex = (_currentLocationIndex - 1 + LevelManager.OpenedLocations.Count) % LevelManager.OpenedLocations.Count;
                _state = SelectionState.DayPart;
                _selectedPartView = null;
                await Init();
                return;
            }

            if (_isNotOpenedLocationShown)
            {
                _currentLocationIndex = LevelManager.OpenedLocations.Count - 1;
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
            if (_useHierarchicalMode && _state == SelectionState.Levels && TryChangeLevelPage(1))
            {
                await Init();
                return;
            }

            if (LevelManager.OpenedLocations.Count == LevelManager.LocationInfoList.locations.Length)
            {
                _currentLocationIndex = (_currentLocationIndex + 1) % LevelManager.OpenedLocations.Count;
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

            if (_currentLocationIndex >= LevelManager.OpenedLocations.Count)
            {
                await ShowNotOpenedLocation();
                _isNotOpenedLocationShown = true;
                _currentLocationIndex = LevelManager.OpenedLocations.Count - 1;
                return;
            }

            _state = SelectionState.DayPart;
            _selectedPartView = null;
            await Init();
        }

        private void OnClickBtnHome(ClickEvent evt)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        private void OnClickBtnBack(ClickEvent evt)
        {
            if (!_useHierarchicalMode)
            {
                return;
            }

            _state = SelectionState.DayPart;
            _selectedPartView = null;
            _currentLevelPage = 0;
            _ = Init();
        }

        private void OnClickBtnSettings(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.SettingsModal);
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
            if (_locationTitle is Label label)
            {
                label.text = text;
            }
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

        private void UpdateLevelPagingButtons(LevelSelectionModel.PartView partView)
        {
            var levels = partView.Levels ?? Array.Empty<LevelSelectionModel.LevelReference>();
            var maxPage = Mathf.Max(0, (levels.Count - 1) / LevelsPerPage);
            _buttonPrevLocation?.SetEnabled(_currentLevelPage > 0);
            _buttonNextLocation?.SetEnabled(_currentLevelPage < maxPage);
        }

        private bool TryChangeLevelPage(int delta)
        {
            if (_selectedPartView == null)
            {
                return false;
            }

            var levels = _selectedPartView.Levels ?? Array.Empty<LevelSelectionModel.LevelReference>();
            if (levels.Count <= LevelsPerPage)
            {
                return false;
            }

            var maxPage = Mathf.Max(0, (levels.Count - 1) / LevelsPerPage);
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
