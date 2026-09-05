using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using Assets.Scripts.Online;
using GameManagement;
using GameManagement.Leaderboard;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает рейтинги каталога и результаты выбранной части дня.
    /// </summary>
    public sealed class LeaderboardScreenController : ScreenController
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 821f;

        // Временный режим визуальной проверки. Включать только для оценки вёрстки.
        private static readonly bool _useVisualQaMockData = false;
        private const int _mockTopCount = 50;

        private static readonly string[] _mockPlayerNames =
        {
            "NeonPaws",
            "ByteRunner",
            "TurboHam",
            "PixelScout",
            "NovaWhisker",
            "CyberMilo",
            "MoonDash",
            "VoltCheeks",
            "GlitchTail",
            "CometNibbler",
            "RocketPip",
            "EchoHam",
            "CircuitBean",
            "LaserPeanut",
            "AstroMochi",
            "NightSpark",
            "ChromePaws",
            "QuantumNut"
        };

        private Label _locationTitle =>
            _contentRoot.Q<Label>("leaderboard__location-title");
        private Button _buttonPreviousLocation =>
            _contentRoot.Q<Button>("leaderboard__location-previous");
        private Button _buttonNextLocation =>
            _contentRoot.Q<Button>("leaderboard__location-next");
        private Button _buttonMorning =>
            _contentRoot.Q<Button>("leaderboard__part-morning");
        private Button _buttonAfternoon =>
            _contentRoot.Q<Button>("leaderboard__part-afternoon");
        private Button _buttonEvening =>
            _contentRoot.Q<Button>("leaderboard__part-evening");
        private Button _buttonNight =>
            _contentRoot.Q<Button>("leaderboard__part-night");
        private Label _loading =>
            _contentRoot.Q<Label>("leaderboard__loading");
        private VisualElement _error =>
            _contentRoot.Q<VisualElement>("leaderboard__error");
        private Button _buttonRetry =>
            _contentRoot.Q<Button>("leaderboard__btn-retry");
        private Label _empty =>
            _contentRoot.Q<Label>("leaderboard__empty");
        private ScrollView _rows =>
            _contentRoot.Q<ScrollView>("leaderboard__rows");
        private VisualElement _currentPlayer =>
            _contentRoot.Q<VisualElement>("leaderboard__current-player");
        private Label _currentRank =>
            _contentRoot.Q<Label>("leaderboard__current-rank");
        private Label _currentName =>
            _contentRoot.Q<Label>("leaderboard__current-name");
        private Label _currentScore =>
            _contentRoot.Q<Label>("leaderboard__current-score");
        private VisualElement _cacheBanner;
        private Label _cacheStatus;
        private Button _cacheRetry;
        private IReadOnlyList<LocationView> _visibleLocations =
            Array.Empty<LocationView>();
        private IReadOnlyList<PartView> _visibleParts =
            Array.Empty<PartView>();
        private LocationView _selectedLocation;
        private PartView _selectedPart;
        private int _currentLocationIndex;
        private int _requestVersion;
        private string _initialLocationId;
        private string _initialPartId;

        protected override ScreenEnum _screenAssetName => ScreenEnum.LeaderboardScreen;

        public LeaderboardScreenController(
            UIDocument uiDocument,
            LeaderboardService leaderboardService)
            : base(uiDocument)
        {
            if (leaderboardService == null) throw new ArgumentNullException(nameof(leaderboardService));
        }

        /// <summary>
        /// Сохраняет цель первого открытия экрана рейтингов.
        /// </summary>
        public void SetInitialSelection(string locationId, string partId)
        {
            _initialLocationId = locationId?.Trim();
            _initialPartId = partId?.Trim();
        }

        protected override string ScreenBackgroundAddress => "LeagueBackgroundSprite";

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return ScreenLayout.Fit(
                content.Q<VisualElement>("leaderboard__viewport"),
                content.Q<VisualElement>("leaderboard__scale-frame"),
                content.Q<VisualElement>("leaderboard__design"),
                new Vector2(DesignWidth, DesignHeight));
        }

        /// <summary>
        /// Размечает выбранный рейтинг и его состояние загрузки.
        /// </summary>
        protected override void BindView()
        {
            // Получаем каталог с единым доменным состоянием доступности.
            _requestVersion++;
            PrepareCacheBanner();
            var selectionModel = LevelSelectionModel.Create();
            _visibleLocations = selectionModel.Locations.ToList();

            // Ищем запрошенную открытую локацию, затем используем первую доступную.
            var requestedLocationIndex = _visibleLocations
                .ToList()
                .FindIndex(location =>
                    IsLocationOpen(location) &&
                    MatchesLocation(location, _initialLocationId));
            _currentLocationIndex = requestedLocationIndex >= 0
                ? requestedLocationIndex
                : _visibleLocations.ToList().FindIndex(IsLocationOpen);
            UpdateLocationArrows();
            if (_currentLocationIndex < 0)
            {
                _selectedLocation = null;
                _selectedPart = null;
                _locationTitle.text = "—";
                _visibleParts = Array.Empty<PartView>();
                UpdatePartButtons(null);
                ShowUnavailable();
                ClearInitialSelection();
                return;
            }

            PrepareLocation(_visibleLocations[_currentLocationIndex]);
        }

        /// <summary>
        /// Открывает локацию и выбирает первую видимую часть дня.
        /// </summary>
        private PartView PrepareLocation(LocationView location)
        {
            if (!IsLocationOpen(location))
                return null;

            _requestVersion++;

            // Показываем все части дня, сохраняя их реальную доступность отдельно.
            _selectedLocation = location;
            _visibleParts = location.Parts.ToList();

            // Обновляем карусель без промежуточного экрана выбора.
            _locationTitle.text = location.DisplayName;

            // Выбираем запрошенную часть, затем утро или первую доступную.
            var requestedPart = MatchesLocation(location, _initialLocationId)
                ? _visibleParts.FirstOrDefault(
                    part =>
                        IsPartOpen(part) &&
                        MatchesPart(part, _initialPartId))
                : null;
            var defaultPart = requestedPart
                              ?? _visibleParts.FirstOrDefault(
                                  part =>
                                      IsPartOpen(part) &&
                                      MatchesPart(part, "morning"))
                              ?? _visibleParts.FirstOrDefault(IsPartOpen);
            UpdatePartButtons(defaultPart);
            ClearInitialSelection();
            if (defaultPart == null)
            {
                _selectedPart = null;
                ShowUnavailable();
                return null;
            }

            _selectedPart = defaultPart;
            ShowLoading();
            return defaultPart;
        }

        protected override Task LoadDataAsync()
        {
            return _selectedPart != null
                ? LoadResultsAsync(_selectedPart)
                : Task.CompletedTask;
        }

        private async Task OpenLocationAsync(LocationView location)
        {
            PartView part = PrepareLocation(location);
            if (part != null)
                await LoadResultsAsync(part);
        }

        private async void OnClickPreviousLocation(ClickEvent evt)
        {
            var previousIndex = _currentLocationIndex - 1;
            if (!CanOpenLocationAt(previousIndex))
                return;

            _currentLocationIndex = previousIndex;
            UpdateLocationArrows();
            await OpenLocationAsync(_visibleLocations[_currentLocationIndex]);
        }

        private async void OnClickNextLocation(ClickEvent evt)
        {
            var nextIndex = _currentLocationIndex + 1;
            if (!CanOpenLocationAt(nextIndex))
                return;

            _currentLocationIndex = nextIndex;
            UpdateLocationArrows();
            await OpenLocationAsync(_visibleLocations[_currentLocationIndex]);
        }

        private async void OnClickMorning(ClickEvent evt)
        {
            await SelectPartAsync("morning");
        }

        private async void OnClickAfternoon(ClickEvent evt)
        {
            await SelectPartAsync("afternoon");
        }

        private async void OnClickEvening(ClickEvent evt)
        {
            await SelectPartAsync("evening");
        }

        private async void OnClickNight(ClickEvent evt)
        {
            await SelectPartAsync("night");
        }

        private async Task SelectPartAsync(string partKey)
        {
            var part = _visibleParts.FirstOrDefault(
                candidate => MatchesPart(candidate, partKey));
            if (part != null && IsPartOpen(part) && part != _selectedPart)
                await LoadResultsAsync(part);
        }

        /// <summary>
        /// Получает серверные результаты и показывает только актуальный ответ.
        /// </summary>
        private async Task LoadResultsAsync(PartView part)
        {
            // Фиксируем выбранную таблицу и показываем загрузку.
            _selectedPart = part;
            UpdatePartButtons(part);

            // Mock полностью обходит сеть и не меняет серверный путь.
            if (_useVisualQaMockData)
            {
                var mockResults = CreateMockResults(part);
                RenderResults(mockResults.Top, mockResults.CurrentPlayer);
                return;
            }

            ShowLoading();
            var requestVersion = ++_requestVersion;
            var owner = GameDataManager.OwnerPlayerId;
            var generation = GameDataManager.Generation;
            var locationId = _selectedLocation.Id;
            var coordinator = WeeklyLeaderboardCoordinator.Instance;
            if (coordinator != null && coordinator.TryGetCachedResults(locationId, part.Id, out var cached))
                RenderCachedResults(cached);

            try
            {
                // Обновляем кеш в фоне, сохраняя доступность уже загруженного рейтинга.
                if (coordinator == null) throw new InvalidOperationException("Leaderboard service is unavailable.");
                var leaderboardResults = await coordinator.GetResultsAsync(locationId, part.Id);
                if (!IsCurrentRequest(requestVersion, owner, generation))
                    return;

                HideCacheBanner();
                RenderResults(
                    leaderboardResults.Top,
                    leaderboardResults.CurrentPlayer);
            }
            catch
            {
                if (!IsCurrentRequest(requestVersion, owner, generation)) return;
                if (coordinator != null && coordinator.TryGetCachedResults(locationId, part.Id, out var fallback))
                    RenderCachedResults(fallback);
                else
                    ShowError();
            }
        }

        private bool IsCurrentRequest(int requestVersion, string owner, long generation) =>
            requestVersion == _requestVersion && owner == GameDataManager.OwnerPlayerId &&
            generation == GameDataManager.Generation;

        /// <summary>Выделяет кеш отдельной строкой с датой загрузки и доступным повтором.</summary>
        private void PrepareCacheBanner()
        {
            _cacheRetry?.UnregisterCallback<ClickEvent>(OnClickRetry);
            _cacheBanner?.RemoveFromHierarchy();
            _cacheBanner = new VisualElement();
            _cacheBanner.style.flexDirection = FlexDirection.Row;
            _cacheBanner.style.alignItems = Align.Center;
            _cacheBanner.style.flexShrink = 0;
            _cacheBanner.style.height = 40;
            _cacheStatus = new Label();
            _cacheStatus.AddToClassList("lcs-text");
            _cacheStatus.style.flexGrow = 1;
            _cacheStatus.style.fontSize = 20;
            _cacheStatus.style.color = (Color)new Color32(20, 18, 15, 255);
            _cacheStatus.style.unityTextOutlineWidth = 0;
            _cacheStatus.style.whiteSpace = WhiteSpace.NoWrap;
            _cacheStatus.style.overflow = Overflow.Hidden;
            _cacheStatus.style.textOverflow = TextOverflow.Ellipsis;
            _cacheRetry = new Button { text = LocalizationManager.GetLocalizedString("leaderboard_retry") };
            _cacheRetry.style.width = 170;
            _cacheRetry.style.fontSize = 20;
            _cacheRetry.RegisterCallback<ClickEvent>(OnClickRetry);
            _cacheBanner.Add(_cacheStatus);
            _cacheBanner.Add(_cacheRetry);
            _rows.parent.Insert(0, _cacheBanner);
            HideCacheBanner();
        }

        private void RenderCachedResults(LeaderboardResultsSnapshot snapshot)
        {
            RenderResults(snapshot.Top, snapshot.CurrentPlayer);
            var timestamp = DateTime.TryParse(snapshot.FetchedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var fetchedAt)
                ? fetchedAt.ToLocalTime().ToString("g") : "—";
            var labelKey = snapshot.IsPreviousWeek ? "leaderboard_cached_previous_week_at" : "leaderboard_cached_at";
            _cacheStatus.text = (LocalizationManager.GetLocalizedString(labelKey) ?? labelKey)
                .Replace("{0}", timestamp);
            _cacheBanner.style.display = DisplayStyle.Flex;
        }

        private void HideCacheBanner()
        {
            if (_cacheBanner != null) _cacheBanner.style.display = DisplayStyle.None;
        }

        private void UpdatePartButtons(PartView selectedPart)
        {
            UpdatePartButton(
                _buttonMorning,
                "morning",
                selectedPart);
            UpdatePartButton(
                _buttonAfternoon,
                "afternoon",
                selectedPart);
            UpdatePartButton(
                _buttonEvening,
                "evening",
                selectedPart);
            UpdatePartButton(
                _buttonNight,
                "night",
                selectedPart);
        }

        private void UpdatePartButton(
            Button button,
            string partKey,
            PartView selectedPart)
        {
            // Оставляем все настроенные части дня видимыми.
            var configuredPart = _visibleParts.FirstOrDefault(
                part => MatchesPart(part, partKey));
            button.parent.style.display = configuredPart == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            var isOpen = configuredPart != null && IsPartOpen(configuredPart);
            button.SetEnabled(isOpen);
            button.style.opacity = isOpen ? 1 : 0.55f;

            // Геометрия вкладок фиксирована внутри общего арт-блока.
            var isSelected = isOpen && configuredPart == selectedPart;
            button.EnableInClassList("leaderboard-part--selected", isSelected);
            button.EnableInClassList(
                "leaderboard-part--available",
                isOpen && !isSelected);
            button.EnableInClassList("leaderboard-part--disabled", !isOpen);
        }

        private bool IsLocationOpen(LocationView location)
        {
            return location?.IsUnlocked == true;
        }

        private bool CanOpenLocationAt(int index)
        {
            return index >= 0 &&
                   index < _visibleLocations.Count &&
                   IsLocationOpen(_visibleLocations[index]);
        }

        private void UpdateLocationArrows()
        {
            UpdateLocationArrow(
                _buttonPreviousLocation,
                CanOpenLocationAt(_currentLocationIndex - 1));
            UpdateLocationArrow(
                _buttonNextLocation,
                CanOpenLocationAt(_currentLocationIndex + 1));
        }

        private static void UpdateLocationArrow(Button button, bool isAvailable)
        {
            button.SetEnabled(isAvailable);
            button.EnableInClassList(
                "leaderboard-location-arrow--disabled",
                !isAvailable);
            button.style.opacity = isAvailable ? 1 : 0.55f;
        }

        private static bool IsPartOpen(PartView part)
        {
            return part?.IsUnlocked == true;
        }

        private static bool MatchesPart(
            PartView part,
            string partKey)
        {
            return string.Equals(
                       part.Id,
                       partKey,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       part.Key,
                       partKey,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesLocation(
            LocationView location,
            string locationId)
        {
            return location != null &&
                   !string.IsNullOrWhiteSpace(locationId) &&
                   (string.Equals(
                        location.Id,
                        locationId,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        location.Key,
                        locationId,
                        StringComparison.OrdinalIgnoreCase));
        }

        private void ClearInitialSelection()
        {
            _initialLocationId = null;
            _initialPartId = null;
        }

        /// <summary>
        /// Создаёт стабильный набор результатов для визуальной проверки экрана.
        /// </summary>
        private (
            IReadOnlyList<LeaderboardEntry> Top,
            LeaderboardEntry CurrentPlayer) CreateMockResults(
            PartView part)
        {
            // Меняем числа между таблицами, сохраняя правдоподобный порядок.
            var tableSeed = StringComparer.OrdinalIgnoreCase.GetHashCode(
                                $"{_selectedLocation.Id}:{part.Id}")
                            & int.MaxValue;
            var scoreOffset = tableSeed % 900;
            var top = new List<LeaderboardEntry>(_mockTopCount);
            for (var index = 0; index < _mockTopCount; index++)
            {
                var baseName = _mockPlayerNames[index % _mockPlayerNames.Length];
                var nameCycle = index / _mockPlayerNames.Length;
                var playerName = nameCycle == 0
                    ? baseName
                    : $"{baseName} {nameCycle + 1}";
                top.Add(new LeaderboardEntry(
                    $"mock-player-{index + 1}",
                    playerName,
                    index,
                    12480 - index * 210 - scoreOffset));
            }

            // Утро показывает подсветку игрока в топе, остальные вкладки — позицию вне топа.
            var currentPlayer = new LeaderboardEntry(
                "mock-current-player",
                "CyberHamster",
                73,
                1560 - scoreOffset / 3);
            if (MatchesPart(part, "morning"))
            {
                const int currentPlayerIndex = 5;
                currentPlayer = new LeaderboardEntry(
                    "mock-current-player",
                    "CyberHamster",
                    currentPlayerIndex,
                    top[currentPlayerIndex].Score);
                top[currentPlayerIndex] = currentPlayer;
            }

            return (top, currentPlayer);
        }

        private void ShowLoading()
        {
            HideCacheBanner();
            _loading.style.display = DisplayStyle.Flex;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.None;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        private void ShowError()
        {
            HideCacheBanner();
            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.Flex;
            _empty.style.display = DisplayStyle.None;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        private void ShowEmpty()
        {
            HideCacheBanner();
            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.Flex;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        private void ShowUnavailable()
        {
            HideCacheBanner();
            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.None;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Заполняет топ-50 и закрепляет результат игрока ниже списка при необходимости.
        /// </summary>
        private void RenderResults(
            IReadOnlyList<LeaderboardEntry> top,
            LeaderboardEntry currentPlayer)
        {
            // Перестраиваем прокручиваемую часть рейтинга.
            _rows.Clear();
            var currentPlayerId = currentPlayer?.PlayerId;
            var currentPlayerInTop = !string.IsNullOrWhiteSpace(currentPlayerId) &&
                                     top.Any(entry => string.Equals(
                                         entry.PlayerId,
                                         currentPlayerId,
                                         StringComparison.Ordinal));
            foreach (var entry in top)
            {
                var isCurrentPlayer = !string.IsNullOrWhiteSpace(currentPlayerId) &&
                                      string.Equals(
                                          entry.PlayerId,
                                          currentPlayerId,
                                          StringComparison.Ordinal);
                _rows.Add(CreateResultRow(entry, isCurrentPlayer));
            }

            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = top.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _rows.style.display = top.Count == 0
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            // Закрепляем результат под разделителем только за пределами показанного топа.
            if (currentPlayer == null || currentPlayerInTop)
            {
                _currentPlayer.style.display = DisplayStyle.None;
                return;
            }

            _currentRank.text = (currentPlayer.Rank + 1).ToString();
            _currentName.text =
                $"{LocalizationManager.GetLocalizedString("leaderboard_you")}: " +
                currentPlayer.PlayerName;
            _currentScore.text = currentPlayer.Score.ToString("0");
            _currentPlayer.style.display = DisplayStyle.Flex;
        }

        private static VisualElement CreateResultRow(
            LeaderboardEntry entry,
            bool isCurrentPlayer)
        {
            // Строка использует колонки и ширину общего центрального блока.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            row.style.height = 56;
            row.style.paddingRight = 20;
            row.style.paddingLeft = 20;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(
                new Color32(198, 162, 104, 255));
            row.AddToClassList(
                isCurrentPlayer
                    ? "leaderboard-result-row--current"
                    : "leaderboard-result-row--default");

            // Выравниваем значения по тем же колонкам, что и заголовок таблицы.
            row.Add(CreateResultLabel((entry.Rank + 1).ToString(), 110));
            row.Add(CreateResultLabel(entry.PlayerName, 0, true));
            row.Add(CreateResultLabel(entry.Score.ToString("0"), 180, false, true));
            return row;
        }

        private static Label CreateResultLabel(
            string text,
            float width,
            bool grow = false,
            bool alignRight = false)
        {
            var label = new Label(text ?? string.Empty);
            label.AddToClassList("lcs-text");
            label.AddToClassList("leaderboard-result-label");
            label.style.fontSize = 26;
            label.style.unityTextOutlineWidth = 0;
            label.style.unityTextAlign = alignRight
                ? TextAnchor.MiddleRight
                : TextAnchor.MiddleLeft;
            label.style.flexGrow = grow ? 1 : 0;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            if (!grow)
                label.style.width = width;

            return label;
        }

        private async void OnClickRetry(ClickEvent evt)
        {
            OnlineServicesCoordinator.RequestRetry(WeeklyLeaderboardCoordinator.RetryKey);
            if (_selectedPart != null)
                await LoadResultsAsync(_selectedPart);
        }

        protected override void OnSubscribeToEvents()
        {
            GameDataManager.ProfileChanged += OnProfileChanged;
            _buttonPreviousLocation?.RegisterCallback<ClickEvent>(
                OnClickPreviousLocation);
            _buttonNextLocation?.RegisterCallback<ClickEvent>(
                OnClickNextLocation);
            _buttonMorning?.RegisterCallback<ClickEvent>(OnClickMorning);
            _buttonAfternoon?.RegisterCallback<ClickEvent>(OnClickAfternoon);
            _buttonEvening?.RegisterCallback<ClickEvent>(OnClickEvening);
            _buttonNight?.RegisterCallback<ClickEvent>(OnClickNight);
            _buttonRetry?.RegisterCallback<ClickEvent>(OnClickRetry);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _requestVersion++;
            GameDataManager.ProfileChanged -= OnProfileChanged;
            _cacheRetry?.UnregisterCallback<ClickEvent>(OnClickRetry);
            _buttonPreviousLocation?.UnregisterCallback<ClickEvent>(
                OnClickPreviousLocation);
            _buttonNextLocation?.UnregisterCallback<ClickEvent>(
                OnClickNextLocation);
            _buttonMorning?.UnregisterCallback<ClickEvent>(OnClickMorning);
            _buttonAfternoon?.UnregisterCallback<ClickEvent>(OnClickAfternoon);
            _buttonEvening?.UnregisterCallback<ClickEvent>(OnClickEvening);
            _buttonNight?.UnregisterCallback<ClickEvent>(OnClickNight);
            _buttonRetry?.UnregisterCallback<ClickEvent>(OnClickRetry);
        }

        private async void OnProfileChanged()
        {
            // Пересобираем доступные локации и исключаем строки прежнего владельца.
            _requestVersion++;
            BindView();
            await LoadDataAsync();
        }
    }
}
