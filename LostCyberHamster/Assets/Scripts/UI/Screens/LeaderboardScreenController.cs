using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.CloudSave;
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
        private Label _errorText => _contentRoot.Q<Label>("leaderboard__error-text");
        private VisualElement _status => _contentRoot.Q<VisualElement>("leaderboard__status");
        private Label _period => _contentRoot.Q<Label>("leaderboard__period");
        private VisualElement _connection => _contentRoot.Q<VisualElement>("leaderboard__connection");
        private Label _connectionText => _contentRoot.Q<Label>("leaderboard__connection-text");
        private Button _buttonRefresh => _contentRoot.Q<Button>("leaderboard__btn-refresh");
        private VisualElement _participation => _contentRoot.Q<VisualElement>("leaderboard__participation");
        private Label _participationText => _contentRoot.Q<Label>("leaderboard__participation-text");
        private Button _buttonParticipation => _contentRoot.Q<Button>("leaderboard__btn-participation");
        private Label _personalStatus => _contentRoot.Q<Label>("leaderboard__personal-status");
        private readonly CloudSyncService _cloudSyncService;
        private LeaderboardReadService _readService;
        private IDisposable _ownershipPrompt;
        private LeaderboardParticipationStatus _participationStatus;
        private string _renderedBoard;
        private int _renderedContentVersion = -1;
        private bool _viewActive;
        private VisualElement _boundContentRoot;
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

        public LeaderboardScreenController(UIDocument uiDocument, CloudSyncService cloudSyncService)
            : base(uiDocument)
        {
            _cloudSyncService = cloudSyncService;
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
                new Vector2(DesignWidth, DesignHeight), stretchWidth: true);
        }

        /// <summary>
        /// Размечает выбранный рейтинг и его состояние загрузки.
        /// </summary>
        protected override void BindView()
        {
            // Получаем каталог с единым доменным состоянием доступности.
            _requestVersion++;
            if (_boundContentRoot != _contentRoot)
            {
                _renderedBoard = null;
                _renderedContentVersion = -1;
                _boundContentRoot = _contentRoot;
            }
            _readService = LeaderboardReadService.Instance;
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
            RenderSelectedSnapshot();
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

        /// <summary>Показывает готовый снимок сразу; сеть обновляет выбранную таблицу в фоне.</summary>
        private async Task LoadResultsAsync(PartView part)
        {
            _selectedPart = part;
            UpdatePartButtons(part);
            if (_useVisualQaMockData)
            {
                var mockResults = CreateMockResults(part);
                RenderResults(mockResults.Top, mockResults.CurrentPlayer);
                _status.style.display = DisplayStyle.None;
                return;
            }

            // Строки кеша появляются без промежуточного состояния загрузки.
            RenderSelectedSnapshot();
            var requestVersion = ++_requestVersion;
            var locationId = _selectedLocation.Id;
            if (_readService == null) return;
            await _readService.RefreshAsync(locationId, part.Id);
            if (_viewActive && requestVersion == _requestVersion)
                RenderSelectedSnapshot();
        }

        /// <summary>Перестраивает строки только при смене таблицы или видимых результатов.</summary>
        private void RenderSelectedSnapshot()
        {
            if (_selectedLocation == null || _selectedPart == null) return;
            var snapshot = _readService?.GetSnapshot(_selectedLocation.Id, _selectedPart.Id);
            if (snapshot == null)
            {
                _status.style.display = DisplayStyle.None;
                _buttonRetry.text = Text("leaderboard_retry");
                ShowError("leaderboard_connection_unavailable");
                return;
            }

            // Состояния подключения и участия обновляются независимо от прокручиваемого списка.
            RenderStatus(snapshot);
            if (!snapshot.HasTable)
            {
                _renderedBoard = null;
                _renderedContentVersion = -1;
                if (snapshot.IsRefreshing || snapshot.Status == LeaderboardReadStatus.Connecting)
                    ShowLoading();
                else
                    ShowError(ConnectionMessageKey(snapshot.Status));
                return;
            }

            // Повтор той же таблицы сохраняет строки и текущую позицию прокрутки.
            if (_renderedBoard != snapshot.LeaderboardId || _renderedContentVersion != snapshot.ContentVersion)
            {
                var sameBoard = _renderedBoard == snapshot.LeaderboardId;
                var scrollOffset = sameBoard ? _rows.scrollOffset : Vector2.zero;
                RenderResults(snapshot.Top, snapshot.CurrentPlayer);
                _rows.scrollOffset = scrollOffset;
                _renderedBoard = snapshot.LeaderboardId;
                _renderedContentVersion = snapshot.ContentVersion;
            }
            _empty.text = Text(snapshot.IsStale ? "leaderboard_cached_empty" : "leaderboard_empty");
        }

        private void RenderStatus(LeaderboardViewSnapshot snapshot)
        {
            _status.style.display = DisplayStyle.Flex;
            _period.text = FormatPeriod(snapshot);
            _participationStatus = snapshot.ParticipationStatus;
            var connectionMessage = snapshot.HasTable && snapshot.Status != LeaderboardReadStatus.Ready &&
                snapshot.Status != LeaderboardReadStatus.Connecting
                ? Text(ConnectionMessageKey(snapshot.Status)) : string.Empty;
            _connectionText.text = connectionMessage;
            _connection.style.display = string.IsNullOrEmpty(connectionMessage) ? DisplayStyle.None : DisplayStyle.Flex;
            _buttonRefresh.style.display = snapshot.HasTable ? DisplayStyle.Flex : DisplayStyle.None;
            var authenticationRequired = snapshot.Status == LeaderboardReadStatus.AuthenticationRequired;
            var recoveryUnavailable = snapshot.Status == LeaderboardReadStatus.ProfileRecoveryUnavailable;
            _buttonRefresh.text = Text(snapshot.IsRefreshing ? "leaderboard_refreshing" :
                authenticationRequired ? "account_reauthenticate" :
                recoveryUnavailable ? "leaderboard_open_profile" : "leaderboard_refresh");
            _buttonRefresh.SetEnabled(!snapshot.IsRefreshing && snapshot.Status != LeaderboardReadStatus.BoardUnavailable);
            _buttonRetry.text = Text(authenticationRequired ? "account_reauthenticate" :
                recoveryUnavailable ? "leaderboard_open_profile" : "leaderboard_retry");
            _buttonRetry.SetEnabled(!snapshot.IsRefreshing);
            _buttonRetry.style.display = snapshot.Status == LeaderboardReadStatus.BoardUnavailable
                ? DisplayStyle.None : DisplayStyle.Flex;

            // Выбор профиля и сохранения влияет на участие, сохраняя доступную таблицу.
            var participationKey = snapshot.ParticipationStatus switch
            {
                LeaderboardParticipationStatus.ProfileRequired => "leaderboard_profile_required",
                LeaderboardParticipationStatus.Synchronizing => "leaderboard_participation_synchronizing",
                LeaderboardParticipationStatus.Conflict => "leaderboard_participation_conflict",
                _ => null
            };
            _participationText.text = participationKey == null ? string.Empty : Text(participationKey);
            _participation.style.display = participationKey == null ? DisplayStyle.None : DisplayStyle.Flex;
            var canChoose = snapshot.ParticipationStatus == LeaderboardParticipationStatus.ProfileRequired ||
                snapshot.ParticipationStatus == LeaderboardParticipationStatus.Conflict;
            _buttonParticipation.style.display = canChoose ? DisplayStyle.Flex : DisplayStyle.None;
            _buttonParticipation.text = Text(snapshot.ParticipationStatus == LeaderboardParticipationStatus.Conflict
                ? "cloud_sync_action_choose" : "profile_ownership_title").ToUpperInvariant();

            // Личный результат, ещё не отправленный забег и отсутствие записи имеют свой текст.
            var personalKey = snapshot.HasTable ? snapshot.PersonalStatus switch
            {
                LeaderboardPersonalStatus.NoEntry => snapshot.IsStale
                    ? "leaderboard_personal_cached_none" : "leaderboard_personal_no_entry",
                LeaderboardPersonalStatus.Unavailable => snapshot.IsRefreshing || snapshot.Status == LeaderboardReadStatus.Connecting
                    ? null : "leaderboard_personal_unavailable",
                _ => null
            } : null;
            var run = snapshot.LatestRun;
            if (run != null)
            {
                personalKey = run.Status switch
                {
                    WeeklyRunStatus.Pending => !snapshot.IsStale && !string.IsNullOrEmpty(snapshot.VersionId) &&
                        snapshot.VersionId != run.VersionId ? "leaderboard_run_previous_week" : "leaderboard_run_pending",
                    WeeklyRunStatus.AwaitingLocalSave => "leaderboard_run_saving",
                    WeeklyRunStatus.Expired => "win_submit_expired",
                    WeeklyRunStatus.Unconfirmed => "win_submit_unconfirmed",
                    WeeklyRunStatus.LocalOnly => run.LocalOnlyReason switch
                    {
                        WeeklyLocalOnlyReason.OwnerUnassigned => "leaderboard_run_local_only_owner",
                        WeeklyLocalOnlyReason.SeasonUnknown => "leaderboard_run_local_only_season",
                        _ => "win_submit_local_only"
                    },
                    _ => personalKey
                };
            }
            if (snapshot.PersonalStatus == LeaderboardPersonalStatus.ProfileRequired)
                personalKey = null;
            _personalStatus.text = personalKey == null ? string.Empty : Text(personalKey);
            _personalStatus.style.display = personalKey == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static string ConnectionMessageKey(LeaderboardReadStatus status) => status switch
        {
            LeaderboardReadStatus.Offline => "leaderboard_offline",
            LeaderboardReadStatus.AuthenticationRequired => "leaderboard_authentication_required",
            LeaderboardReadStatus.ProfileRecoveryUnavailable => "leaderboard_profile_recovery_unavailable",
            LeaderboardReadStatus.BoardUnavailable => "leaderboard_board_unavailable",
            _ => "leaderboard_connection_unavailable"
        };

        private static string FormatPeriod(LeaderboardViewSnapshot snapshot)
        {
            if (!snapshot.HasTable) return Text("leaderboard_weekly");
            var culture = CultureInfo.GetCultureInfo(LocalizationManager.CurrentLanguage == SystemLanguage.Russian
                ? "ru-RU" : "en-US");
            var period = Text(snapshot.IsStale ? "leaderboard_saved" : "leaderboard_weekly");
            if (DateTime.TryParse(snapshot.NextResetUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var nextReset))
            {
                var previous = nextReset.ToUniversalTime() <= DateTime.UtcNow;
                period = Text(previous ? "leaderboard_previous_week_range" : "leaderboard_week_range")
                    .Replace("{0}", nextReset.AddDays(-7).ToLocalTime().ToString("d MMM", culture))
                    .Replace("{1}", nextReset.ToLocalTime().ToString("d MMM", culture));
            }
            if (snapshot.IsStale && DateTime.TryParse(snapshot.FetchedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var fetchedAt))
                period += " · " + Text("leaderboard_saved_at").Replace("{0}", fetchedAt.ToLocalTime().ToString("g", culture));
            return period;
        }

        private static string Text(string key) => LocalizationManager.GetLocalizedString(key) ?? key;

        private void OnResultsChanged(string boardId)
        {
            if (!_viewActive || _selectedLocation == null || _selectedPart == null) return;
            var selectedBoard = LeaderboardService.ResolveLeaderboardId(_selectedLocation.Id, _selectedPart.Id);
            if (string.IsNullOrEmpty(boardId) || boardId == selectedBoard)
                RenderSelectedSnapshot();
        }

        private void OnClickParticipation(ClickEvent evt)
        {
            if (_participationStatus == LeaderboardParticipationStatus.Conflict)
            {
                _cloudSyncService?.ShowConflict();
                return;
            }
            if (ProfileOwnershipService.Instance?.CanAdoptGuestProgress != true)
            {
                SettingsScreenController.OpenProfileChoiceFrom(ScreenEnum.LeaderboardScreen);
                return;
            }
            _ownershipPrompt?.Dispose();
            _ownershipPrompt = ProfileOwnershipPrompt.Show(_contentRoot,
                () => SettingsScreenController.OpenExistingAccountFrom(ScreenEnum.LeaderboardScreen));
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
            _loading.style.display = DisplayStyle.Flex;
            _error.style.display = DisplayStyle.None;
            _empty.style.display = DisplayStyle.None;
            _rows.style.display = DisplayStyle.None;
            _currentPlayer.style.display = DisplayStyle.None;
        }

        private void ShowError(string messageKey)
        {
            _loading.style.display = DisplayStyle.None;
            _error.style.display = DisplayStyle.Flex;
            _errorText.text = Text(messageKey);
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

        private void ShowUnavailable()
        {
            _status.style.display = DisplayStyle.None;
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
            if (_selectedPart == null || _selectedLocation == null) return;
            var snapshot = _readService?.GetSnapshot(_selectedLocation.Id, _selectedPart.Id);
            if (snapshot?.Status == LeaderboardReadStatus.AuthenticationRequired ||
                snapshot?.Status == LeaderboardReadStatus.ProfileRecoveryUnavailable)
            {
                SettingsScreenController.OpenProfileChoiceFrom(ScreenEnum.LeaderboardScreen);
                return;
            }
            await LoadResultsAsync(_selectedPart);
        }

        protected override void OnSubscribeToEvents()
        {
            _viewActive = true;
            GameDataManager.ProfileChanged += OnProfileChanged;
            if (_readService != null) _readService.ResultsChanged += OnResultsChanged;
            _buttonRefresh?.RegisterCallback<ClickEvent>(OnClickRetry);
            _buttonParticipation?.RegisterCallback<ClickEvent>(OnClickParticipation);
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
            _viewActive = false;
            GameDataManager.ProfileChanged -= OnProfileChanged;
            if (_readService != null) _readService.ResultsChanged -= OnResultsChanged;
            _buttonRefresh?.UnregisterCallback<ClickEvent>(OnClickRetry);
            _buttonParticipation?.UnregisterCallback<ClickEvent>(OnClickParticipation);
            _ownershipPrompt?.Dispose();
            _ownershipPrompt = null;
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
            _initialLocationId = _selectedLocation?.Id;
            _initialPartId = _selectedPart?.Id;
            BindView();
            await LoadDataAsync();
        }
    }
}
