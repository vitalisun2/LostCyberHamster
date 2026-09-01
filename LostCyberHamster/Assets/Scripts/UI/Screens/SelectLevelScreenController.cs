using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public sealed class SelectLevelScreenController : ScreenController
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;
        private const int LevelSlotCount = 9;
        private const string BackgroundAssetName =
            "SelectLevelScreenBackgroundSprite";

        private static readonly string[] LocationVisualClasses =
        {
            "select-time-location--new-york",
            "select-time-location--paris",
            "select-time-location--barcelona"
        };

        private enum SelectionState
        {
            DayPart,
            Levels
        }

        private VisualElement Viewport =>
            _contentRoot.Q<VisualElement>("select-level-viewport");
        private VisualElement ScaleFrame =>
            _contentRoot.Q<VisualElement>("select-level-scale-frame");
        private VisualElement Design =>
            _contentRoot.Q<VisualElement>("select-level-design");
        private VisualElement DayPartState =>
            _contentRoot.Q<VisualElement>("select-time-state");
        private VisualElement LevelsState =>
            _contentRoot.Q<VisualElement>("select-level-state");
        private VisualElement DayPartsContainer =>
            _contentRoot.Q<VisualElement>("day-parts-container");
        private VisualElement LevelCardsContainer =>
            _contentRoot.Q<VisualElement>("level-cards-container");
        private VisualElement PreviousLocationBadge =>
            _contentRoot.Q<VisualElement>("select-time-location-previous");
        private VisualElement CurrentLocationBadge =>
            _contentRoot.Q<VisualElement>("select-time-location-current");
        private VisualElement NextLocationBadge =>
            _contentRoot.Q<VisualElement>("select-time-location-next");
        private Label LevelHeaderLabel =>
            _contentRoot.Q<Label>("select-level-header-label");
        private Button BackButton =>
            _contentRoot.Q<Button>("btn_select-level-back");
        private Button NextLocationButton =>
            _contentRoot.Q<Button>("btn__location-next");
        private Button PreviousLocationButton =>
            _contentRoot.Q<Button>("btn__location-prev");

        private int _currentLocationIndex = LevelManager.GetLocationIndex();
        private LevelSelectionModel _selectionModel;
        private LocationView _selectedLocationView;
        private PartView _selectedPartView;
        private SelectionState _state = SelectionState.DayPart;

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.SelectLevelScreen;

        public SelectLevelScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync(
                BackgroundAssetName,
                ScaleMode.ScaleAndCrop);

            // Восстанавливаем состояние выбора из текущего прогресса.
            InitializeSelectionModel();
            RenderCurrentState();
        }

        protected override void OnSubscribeToEvents()
        {
            BackButton?.RegisterCallback<ClickEvent>(OnBackClicked);
            NextLocationButton?.RegisterCallback<ClickEvent>(
                OnNextLocationClicked);
            PreviousLocationButton?.RegisterCallback<ClickEvent>(
                OnPreviousLocationClicked);
            Viewport?.RegisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
            Viewport?.schedule.Execute(
                () => ApplyResponsiveLayout(Viewport.contentRect.size));
        }

        protected override void OnUnsubscribeFromEvents()
        {
            BackButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
            NextLocationButton?.UnregisterCallback<ClickEvent>(
                OnNextLocationClicked);
            PreviousLocationButton?.UnregisterCallback<ClickEvent>(
                OnPreviousLocationClicked);
            Viewport?.UnregisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
        }

        private void InitializeSelectionModel()
        {
            _selectionModel = LevelSelectionModel.Create();
            int locationCount = _selectionModel.Locations.Count;
            _currentLocationIndex = locationCount > 0
                ? Mathf.Clamp(_currentLocationIndex, 0, locationCount - 1)
                : 0;
            _selectedLocationView = GetCurrentLocationView();
            _selectedPartView = null;
            _state = SelectionState.DayPart;
        }

        private void RenderCurrentState()
        {
            bool showLevels =
                _state == SelectionState.Levels &&
                _selectedLocationView != null &&
                _selectedPartView != null;

            DayPartState.style.display = showLevels
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            LevelsState.style.display = showLevels
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (showLevels)
            {
                RenderLevels();
                return;
            }

            _state = SelectionState.DayPart;
            _selectedPartView = null;
            _selectedLocationView = GetCurrentLocationView();
            RenderDayParts();
        }

        private void RenderDayParts()
        {
            UpdateLocationSelector();
            DayPartsContainer.Clear();

            IReadOnlyList<PartView> parts =
                _selectedLocationView?.Parts ?? Array.Empty<PartView>();
            foreach (PartView partView in parts)
            {
                bool isUnlocked =
                    _selectedLocationView.IsUnlocked && partView.IsUnlocked;
                var levelItem = new LevelItem();
                levelItem.ConfigureForPart(
                    partView.Key,
                    ResolveDayPartTitle(
                        partView.Key,
                        partView.DisplayName),
                    isUnlocked);
                DayPartsContainer.Add(levelItem);

                if (!levelItem.IsLocked)
                {
                    LocationView locationCapture = _selectedLocationView;
                    PartView partCapture = partView;
                    levelItem.RegisterCallback<ClickEvent>(evt =>
                        OnDayPartClicked(
                            evt,
                            locationCapture,
                            partCapture));
                }
            }
        }

        private void RenderLevels()
        {
            LevelCardsContainer.Clear();
            IReadOnlyList<LevelProgress> levels =
                _selectedPartView.Levels ?? Array.Empty<LevelProgress>();

            // Раскладываем catalog order вдоль готового маршрута-змейки.
            for (int catalogIndex = 0;
                 catalogIndex < LevelSlotCount;
                 catalogIndex++)
            {
                var levelItem = new LevelItem();
                levelItem.AddToClassList(
                    $"select-level-card-slot--{catalogIndex + 1}");

                if (catalogIndex < levels.Count)
                {
                    LevelProgress level = levels[catalogIndex];
                    levelItem.ConfigureForLevel(level, catalogIndex + 1);
                    if (!levelItem.IsLocked &&
                        !string.IsNullOrWhiteSpace(levelItem.LevelName))
                    {
                        string levelNameCapture = levelItem.LevelName;
                        levelItem.RegisterCallback<ClickEvent>(evt =>
                            OnLevelClicked(evt, levelNameCapture));
                    }
                }
                else
                {
                    levelItem.ConfigureLockedPlaceholder();
                }

                LevelCardsContainer.Add(levelItem);
            }

            // Локализуем заголовок и ужимаем только длинные варианты.
            string locationTitle = ResolveLocalizedTitle(
                _selectedLocationView.Key,
                _selectedLocationView.DisplayName);
            string partTitle = ResolveDayPartTitle(
                _selectedPartView.Key,
                _selectedPartView.DisplayName);
            LevelHeaderLabel.text =
                $"{locationTitle} — {partTitle}".ToUpperInvariant();
            LevelHeaderLabel.EnableInClassList(
                "select-level-header__label--compact",
                LevelHeaderLabel.text.Length > 22);
        }

        private void UpdateLocationSelector()
        {
            int locationCount = _selectionModel?.Locations.Count ?? 0;
            if (locationCount == 0)
            {
                SetLocationBadge(PreviousLocationBadge, null);
                SetLocationBadge(CurrentLocationBadge, null);
                SetLocationBadge(NextLocationBadge, null);
                PreviousLocationButton?.SetEnabled(false);
                NextLocationButton?.SetEnabled(false);
                return;
            }

            SetLocationBadge(
                PreviousLocationBadge,
                locationCount > 1
                    ? GetLocationView(_currentLocationIndex - 1)
                    : null);
            SetLocationBadge(
                CurrentLocationBadge,
                GetLocationView(_currentLocationIndex));
            SetLocationBadge(
                NextLocationBadge,
                locationCount > 1
                    ? GetLocationView(_currentLocationIndex + 1)
                    : null);

            PreviousLocationButton?.SetEnabled(locationCount > 1);
            NextLocationButton?.SetEnabled(locationCount > 1);
        }

        private static void SetLocationBadge(
            VisualElement badge,
            LocationView location)
        {
            if (badge == null)
            {
                return;
            }

            foreach (string className in LocationVisualClasses)
            {
                badge.RemoveFromClassList(className);
            }

            badge.style.display = location == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            if (location == null)
            {
                return;
            }

            string visualClass = location.Index switch
            {
                0 => "select-time-location--new-york",
                1 => "select-time-location--paris",
                2 => "select-time-location--barcelona",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(visualClass))
            {
                badge.AddToClassList(visualClass);
            }

            badge.EnableInClassList(
                "select-time-location--locked",
                !location.IsUnlocked);
            Label label = badge.Q<Label>(
                className: "select-time-location__label");
            if (label != null)
            {
                label.text = ResolveLocalizedTitle(
                        location.Key,
                        location.DisplayName)
                    .ToUpperInvariant();
            }
        }

        private LocationView GetCurrentLocationView()
        {
            return GetLocationView(_currentLocationIndex);
        }

        private LocationView GetLocationView(int index)
        {
            int locationCount = _selectionModel?.Locations.Count ?? 0;
            if (locationCount == 0)
            {
                return null;
            }

            int wrappedIndex = (index % locationCount + locationCount) %
                               locationCount;
            return _selectionModel.Locations[wrappedIndex];
        }

        private void OnDayPartClicked(
            ClickEvent evt,
            LocationView locationView,
            PartView partView)
        {
            evt.StopPropagation();
            _selectedLocationView = locationView;
            _selectedPartView = partView;
            _state = SelectionState.Levels;
            RenderCurrentState();
        }

        private static void OnLevelClicked(
            ClickEvent evt,
            string levelName)
        {
            evt.StopPropagation();
            LevelController.Instance.SetCurrentLevel(levelName);
            SceneManager.LoadScene("Game");
        }

        private void OnPreviousLocationClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ChangeLocation(-1);
        }

        private void OnNextLocationClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ChangeLocation(1);
        }

        private void ChangeLocation(int delta)
        {
            int locationCount = _selectionModel?.Locations.Count ?? 0;
            if (locationCount <= 1)
            {
                return;
            }

            _currentLocationIndex =
                (_currentLocationIndex + delta + locationCount) %
                locationCount;
            _selectedLocationView = GetCurrentLocationView();
            RenderDayParts();
        }

        private void OnBackClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            _state = SelectionState.DayPart;
            _selectedPartView = null;
            RenderCurrentState();
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

        private static string ResolveDayPartTitle(
            string key,
            string fallback)
        {
            string localized = ResolveLocalizedTitle(key, fallback);
            if (string.Equals(
                    key,
                    "Afternoon",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    localized,
                    "Afternoon",
                    StringComparison.OrdinalIgnoreCase))
            {
                localized = "Day";
            }

            return localized.ToUpperInvariant();
        }

        /// <summary>
        /// Возвращает локализованный заголовок с резервным отображаемым именем.
        /// </summary>
        private static string ResolveLocalizedTitle(
            string key,
            string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            string localized = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(localized) ||
                   string.Equals(localized, key, StringComparison.Ordinal)
                ? fallback ?? key
                : localized;
        }
    }
}
