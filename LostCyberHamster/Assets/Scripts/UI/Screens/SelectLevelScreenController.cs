using System;
using System.Collections.Generic;
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
        private const float LocationSwipeDistance = 72f;
        private const float LocationSwipeAxisRatio = 1.25f;
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
        private VisualElement _locationSwipeSurface;
        private int _swipePointerId = -1;
        private int _suppressedClickPointerId = -1;
        private Vector2 _swipeStart;
        private bool _isLocationSwiping;

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.SelectLevelScreen;

        public SelectLevelScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        protected override string ScreenBackgroundAddress => BackgroundAssetName;

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return ScreenLayout.Fit(
                content.Q<VisualElement>("select-level-viewport"),
                content.Q<VisualElement>("select-level-scale-frame"),
                content.Q<VisualElement>("select-level-design"),
                new Vector2(DesignWidth, DesignHeight));
        }

        protected override void BindView()
        {
            // Восстанавливаем состояние выбора из текущего прогресса.
            InitializeSelectionModel();
            ApplyNextGoalTarget();
            RenderCurrentState();
        }

        /// <summary>Применяет цель и при переходе внутри уже открытого Select Level.</summary>
        internal void ShowNextGoalTarget()
        {
            InitializeSelectionModel();
            ApplyNextGoalTarget();
            RenderCurrentState();
        }

        /// <summary>Открывает существующую сетку нужной части суток по адресу цели.</summary>
        private void ApplyNextGoalTarget()
        {
            if (!NextGoalNavigation.TryConsume(ScreenEnum.SelectLevelScreen, out var goal)) return;
            string address = NextGoalNavigation.GetStageAddress(goal);
            if (string.IsNullOrEmpty(address)) return;

            // Доступность повторно берём из модели уже подготовленного экрана.
            for (int locationIndex = 0; locationIndex < _selectionModel.Locations.Count; locationIndex++)
            {
                var location = _selectionModel.Locations[locationIndex];
                if (!location.IsUnlocked) continue;
                foreach (var part in location.Parts)
                {
                    if (!part.IsUnlocked) continue;
                    foreach (var level in part.Levels)
                    {
                        if (!level.IsUnlocked || !string.Equals(level.Address, address, StringComparison.Ordinal)) continue;
                        _currentLocationIndex = locationIndex;
                        _selectedLocationView = location;
                        _selectedPartView = part;
                        _state = SelectionState.Levels;
                        return;
                    }
                }
            }
        }

        protected override void OnSubscribeToEvents()
        {
            BackButton?.RegisterCallback<ClickEvent>(OnBackClicked);
            NextLocationButton?.RegisterCallback<ClickEvent>(
                OnNextLocationClicked);
            PreviousLocationButton?.RegisterCallback<ClickEvent>(
                OnPreviousLocationClicked);
            _locationSwipeSurface = DayPartState;
            _contentRoot.RegisterCallback<PointerDownEvent>(
                OnLocationPointerDown, TrickleDown.TrickleDown);
            SubscribeToSwipePointerEvents(_contentRoot);
            SubscribeToSwipePointerEvents(PreviousLocationButton);
            SubscribeToSwipePointerEvents(NextLocationButton);
            _contentRoot.RegisterCallback<PointerCaptureOutEvent>(
                OnLocationPointerCaptureOut, TrickleDown.TrickleDown);
            _contentRoot.RegisterCallback<ClickEvent>(
                OnLocationClick, TrickleDown.TrickleDown);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            BackButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
            NextLocationButton?.UnregisterCallback<ClickEvent>(
                OnNextLocationClicked);
            PreviousLocationButton?.UnregisterCallback<ClickEvent>(
                OnPreviousLocationClicked);
            _contentRoot.UnregisterCallback<PointerDownEvent>(
                OnLocationPointerDown, TrickleDown.TrickleDown);
            UnsubscribeFromSwipePointerEvents(_contentRoot);
            UnsubscribeFromSwipePointerEvents(PreviousLocationButton);
            UnsubscribeFromSwipePointerEvents(NextLocationButton);
            _contentRoot.UnregisterCallback<PointerCaptureOutEvent>(
                OnLocationPointerCaptureOut, TrickleDown.TrickleDown);
            _contentRoot.UnregisterCallback<ClickEvent>(
                OnLocationClick, TrickleDown.TrickleDown);
            ResetLocationSwipe();
            _suppressedClickPointerId = -1;
            _locationSwipeSurface = null;
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

            // Размещаем существующие уровни на прежних позициях маршрута 3×3.
            int visibleLevelCount = Mathf.Min(levels.Count, LevelSlotCount);
            for (int catalogIndex = 0;
                 catalogIndex < visibleLevelCount;
                 catalogIndex++)
            {
                var levelItem = new LevelItem();
                levelItem.AddToClassList(
                    $"select-level-card-slot--{catalogIndex + 1}");

                LevelProgress level = levels[catalogIndex];
                levelItem.ConfigureForLevel(level, catalogIndex + 1);
                if (!levelItem.IsLocked &&
                    !string.IsNullOrWhiteSpace(levelItem.LevelName))
                {
                    string levelNameCapture = levelItem.LevelName;
                    levelItem.RegisterCallback<ClickEvent>(evt =>
                        OnLevelClicked(evt, levelNameCapture));
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

            PreviousLocationButton?.SetEnabled(
                CanNavigateToLocation(_currentLocationIndex - 1));
            NextLocationButton?.SetEnabled(
                CanNavigateToLocation(_currentLocationIndex + 1));
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

        private bool CanNavigateToLocation(int index)
        {
            int locationCount = _selectionModel?.Locations.Count ?? 0;
            return locationCount > 1 &&
                   GetLocationView(index)?.IsUnlocked == true;
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

        private void OnLocationPointerDown(PointerDownEvent evt)
        {
            if (!evt.isPrimary || evt.button != 0)
                return;

            // Новое нажатие завершает прежний жест и возвращает обычные клики.
            ResetLocationSwipe();
            _suppressedClickPointerId = -1;
            if (_state != SelectionState.DayPart ||
                _locationSwipeSurface == null ||
                evt.target is not VisualElement target ||
                (target != _locationSwipeSurface &&
                 !_locationSwipeSurface.Contains(target)))
                return;

            // Координаты макета учитывают масштаб экрана и PanelSettings.
            _swipePointerId = evt.pointerId;
            _swipeStart = _locationSwipeSurface.WorldToLocal(evt.position);
        }

        private void SubscribeToSwipePointerEvents(VisualElement element)
        {
            // В Unity 6.2 захват кнопкой направляет события только самой кнопке.
            element?.RegisterCallback<PointerMoveEvent>(
                OnLocationPointerMove, TrickleDown.TrickleDown);
            element?.RegisterCallback<PointerUpEvent>(
                OnLocationPointerUp, TrickleDown.TrickleDown);
            element?.RegisterCallback<PointerCancelEvent>(
                OnLocationPointerCancel, TrickleDown.TrickleDown);
        }

        private void UnsubscribeFromSwipePointerEvents(VisualElement element)
        {
            element?.UnregisterCallback<PointerMoveEvent>(
                OnLocationPointerMove, TrickleDown.TrickleDown);
            element?.UnregisterCallback<PointerUpEvent>(
                OnLocationPointerUp, TrickleDown.TrickleDown);
            element?.UnregisterCallback<PointerCancelEvent>(
                OnLocationPointerCancel, TrickleDown.TrickleDown);
        }

        private void OnLocationPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _swipePointerId)
                return;

            Vector2 delta = (Vector2)_locationSwipeSurface.WorldToLocal(
                evt.position) - _swipeStart;
            if (!_isLocationSwiping && IsLocationSwipe(delta))
            {
                _isLocationSwiping = true;
                _suppressedClickPointerId = evt.pointerId;
                _contentRoot.CapturePointer(evt.pointerId);
            }

            if (_isLocationSwiping)
                evt.StopImmediatePropagation();
        }

        private void OnLocationPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _swipePointerId)
                return;

            Vector2 delta = (Vector2)_locationSwipeSurface.WorldToLocal(
                evt.position) - _swipeStart;
            bool completedSwipe = IsLocationSwipe(delta);
            bool consumed = _isLocationSwiping || completedSwipe;
            if (consumed && !_isLocationSwiping)
                _contentRoot.CapturePointer(evt.pointerId);
            ResetLocationSwipe();
            if (!consumed)
                return;

            // ClickEvent может прийти следом за PointerUp даже после захвата.
            _suppressedClickPointerId = evt.pointerId;
            evt.StopImmediatePropagation();
            if (completedSwipe && _state == SelectionState.DayPart)
                ChangeLocation(delta.x < 0f ? 1 : -1);
        }

        private static bool IsLocationSwipe(Vector2 delta)
        {
            return Mathf.Abs(delta.x) >= LocationSwipeDistance &&
                   Mathf.Abs(delta.x) >=
                   Mathf.Abs(delta.y) * LocationSwipeAxisRatio;
        }

        private void OnLocationPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _swipePointerId)
                ResetLocationSwipe();
        }

        private void OnLocationPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _swipePointerId &&
                evt.target == _contentRoot)
                ResetLocationSwipe();
        }

        private void OnLocationClick(ClickEvent evt)
        {
            if (evt.pointerId == _suppressedClickPointerId)
                evt.StopImmediatePropagation();
        }

        private void ResetLocationSwipe()
        {
            int pointerId = _swipePointerId;
            _swipePointerId = -1;
            _isLocationSwiping = false;
            if (pointerId >= 0 &&
                _contentRoot.HasPointerCapture(pointerId))
                _contentRoot.ReleasePointer(pointerId);
        }

        private void ChangeLocation(int delta)
        {
            int locationCount = _selectionModel?.Locations.Count ?? 0;
            if (locationCount <= 1 ||
                !CanNavigateToLocation(_currentLocationIndex + delta))
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
