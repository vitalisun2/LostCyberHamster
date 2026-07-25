using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
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
        // Временный режим визуальной проверки. Для возврата к серверным данным сменить на false.
        private static readonly bool _useVisualQaMockData = true;
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
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");
        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");
        private Button _buttonAddMoney =>
            _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button _buttonAddCrystals =>
            _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;

        private readonly LeaderboardService _leaderboardService;
        private IReadOnlyList<LevelSelectionModel.LocationView> _visibleLocations =
            Array.Empty<LevelSelectionModel.LocationView>();
        private IReadOnlyList<LevelSelectionModel.PartView> _visibleParts =
            Array.Empty<LevelSelectionModel.PartView>();
        private LevelSelectionModel.LocationView _selectedLocation;
        private LevelSelectionModel.PartView _selectedPart;
        private int _currentLocationIndex;
        private int _requestVersion;

        protected override ScreenEnum _screenAssetName => ScreenEnum.LeaderboardScreen;

        public LeaderboardScreenController(
            UIDocument uiDocument,
            LeaderboardService leaderboardService)
            : base(uiDocument)
        {
            _leaderboardService = leaderboardService
                ?? throw new ArgumentNullException(nameof(leaderboardService));
        }

        /// <summary>
        /// Загружает фон и сразу открывает рейтинг первой локации каталога.
        /// </summary>
        protected override async Task OnLoadAsync()
        {
            // Собираем локации для текущего режима в порядке игрового каталога.
            _requestVersion++;
            var selectionModel = LevelSelectionModel.Create();
            var openedLocationCount = Math.Min(
                LevelManager.OpenedLocations.Count,
                selectionModel.Locations.Count);
            _visibleLocations = _useVisualQaMockData
                ? selectionModel.Locations.ToList()
                : selectionModel.Locations.Take(openedLocationCount).ToList();

            await ChangeBackgroundAsync("BackgroundScreenSprite");

            // Карусель нужна только когда в каталоге больше одной видимой локации.
            var hasMultipleLocations = _visibleLocations.Count > 1;
            _buttonPreviousLocation.SetEnabled(hasMultipleLocations);
            _buttonNextLocation.SetEnabled(hasMultipleLocations);

            // Экран всегда начинает с первой локации и вкладки утра.
            if (_visibleLocations.Count == 0)
            {
                _selectedLocation = null;
                _selectedPart = null;
                _locationTitle.text = "—";
                _visibleParts = Array.Empty<LevelSelectionModel.PartView>();
                UpdatePartButtons(null);
                ShowEmpty();
                return;
            }

            _currentLocationIndex = 0;
            await OpenLocationAsync(_visibleLocations[_currentLocationIndex]);
        }

        /// <summary>
        /// Открывает локацию и выбирает первую видимую часть дня.
        /// </summary>
        private async Task OpenLocationAsync(LevelSelectionModel.LocationView location)
        {
            _requestVersion++;

            // Собираем части дня для текущего режима.
            _selectedLocation = location;
            _visibleParts = _useVisualQaMockData
                ? location.Parts.ToList()
                : location.Parts
                    .Where(part => part.Levels.Any(
                        level => LevelManager.IsLevelOpen(level.Address)))
                    .ToList();

            // Обновляем карусель без промежуточного экрана выбора.
            _locationTitle.text = location.DisplayName;

            // Выбираем утро или первую видимую часть дня.
            var defaultPart = _visibleParts.FirstOrDefault(
                                  part => MatchesPart(part, "morning"))
                              ?? _visibleParts.FirstOrDefault();
            UpdatePartButtons(defaultPart);
            if (defaultPart == null)
            {
                _selectedPart = null;
                ShowEmpty();
                return;
            }

            await LoadResultsAsync(defaultPart);
        }

        private async void OnClickPreviousLocation(ClickEvent evt)
        {
            if (_visibleLocations.Count <= 1)
                return;

            _currentLocationIndex =
                (_currentLocationIndex - 1 + _visibleLocations.Count) %
                _visibleLocations.Count;
            await OpenLocationAsync(_visibleLocations[_currentLocationIndex]);
        }

        private async void OnClickNextLocation(ClickEvent evt)
        {
            if (_visibleLocations.Count <= 1)
                return;

            _currentLocationIndex =
                (_currentLocationIndex + 1) % _visibleLocations.Count;
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
            if (part != null && part != _selectedPart)
                await LoadResultsAsync(part);
        }

        /// <summary>
        /// Получает серверные результаты и показывает только актуальный ответ.
        /// </summary>
        private async Task LoadResultsAsync(LevelSelectionModel.PartView part)
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

            try
            {
                // Получаем топ и отдельную позицию текущего игрока.
                var leaderboardResults = await _leaderboardService.GetResultsAsync(
                    _selectedLocation.Id,
                    part.Id);
                if (requestVersion != _requestVersion)
                    return;

                RenderResults(
                    leaderboardResults.Top,
                    leaderboardResults.CurrentPlayer);
            }
            catch
            {
                if (requestVersion == _requestVersion)
                    ShowError();
            }
        }

        private void UpdatePartButtons(LevelSelectionModel.PartView selectedPart)
        {
            UpdatePartButton(_buttonMorning, "morning", selectedPart);
            UpdatePartButton(_buttonAfternoon, "afternoon", selectedPart);
            UpdatePartButton(_buttonEvening, "evening", selectedPart);
            UpdatePartButton(_buttonNight, "night", selectedPart);
        }

        private void UpdatePartButton(
            Button button,
            string partKey,
            LevelSelectionModel.PartView selectedPart)
        {
            // Скрываем недоступные части дня в реальном режиме.
            var visiblePart = _visibleParts.FirstOrDefault(
                part => MatchesPart(part, partKey));
            button.style.display = visiblePart == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            button.SetEnabled(visiblePart != null);

            // Активный таб соединяем с левой границей таблицы.
            var isSelected = visiblePart == selectedPart;
            button.EnableInClassList("bg-warning", isSelected);
            button.EnableInClassList("bg-primary", !isSelected);
            button.style.width = isSelected ? 244 : 220;
            button.style.marginRight = isSelected ? 0 : 12;
            button.style.borderRightWidth = isSelected ? 0 : 6;
            button.style.borderTopRightRadius = isSelected ? 0 : 24;
            button.style.borderBottomRightRadius = isSelected ? 0 : 24;
        }

        private static bool MatchesPart(
            LevelSelectionModel.PartView part,
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

        /// <summary>
        /// Создаёт стабильный набор результатов для визуальной проверки экрана.
        /// </summary>
        private (
            IReadOnlyList<LeaderboardEntry> Top,
            LeaderboardEntry CurrentPlayer) CreateMockResults(
            LevelSelectionModel.PartView part)
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
            _loading.style.display = DisplayStyle.Flex;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.None;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        private void ShowError()
        {
            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.Flex;
            _empty.style.display = DisplayStyle.None;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        private void ShowEmpty()
        {
            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.Flex;
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
            // Создаём компактную строку, чтобы в панели помещалось около десяти мест.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            row.style.height = 54;
            row.style.paddingRight = 20;
            row.style.paddingLeft = 20;
            row.style.marginBottom = 4;
            row.style.backgroundColor = isCurrentPlayer
                ? new Color(1f, 0.89f, 0.36f, 0.9f)
                : new Color(1f, 1f, 1f, 0.78f);

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
            label.style.fontSize = 26;
            label.style.color = new Color(
                50f / 255f,
                43f / 255f,
                34f / 255f);
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

        private void OnClickHome(ClickEvent evt)
        {
            OpenHome();
        }

        private void OnClickSettings(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.SettingsModal);
        }

        private void OnClickShop(ClickEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }

        private async void OnClickRetry(ClickEvent evt)
        {
            if (_selectedPart != null)
                await LoadResultsAsync(_selectedPart);
        }

        private void OpenHome()
        {
            _requestVersion++;
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonHome?.RegisterCallback<ClickEvent>(OnClickHome);
            _buttonSettings?.RegisterCallback<ClickEvent>(OnClickSettings);
            _buttonAddMoney?.RegisterCallback<ClickEvent>(OnClickShop);
            _buttonAddCrystals?.RegisterCallback<ClickEvent>(OnClickShop);
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
            _buttonHome?.UnregisterCallback<ClickEvent>(OnClickHome);
            _buttonSettings?.UnregisterCallback<ClickEvent>(OnClickSettings);
            _buttonAddMoney?.UnregisterCallback<ClickEvent>(OnClickShop);
            _buttonAddCrystals?.UnregisterCallback<ClickEvent>(OnClickShop);
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
    }
}
