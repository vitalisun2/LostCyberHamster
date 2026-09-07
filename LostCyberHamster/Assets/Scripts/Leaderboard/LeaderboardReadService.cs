using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Online;
using Assets.Scripts.System;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Models;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement.Leaderboard
{
    /// <summary>Показывает кеш сразу, обновляет таблицы в фоне и сохраняет независимость просмотра от прогресса.</summary>
    public sealed class LeaderboardReadService : IDisposable
    {
        private const string RetryKey = "leaderboard-read";
        private readonly AccountService _account;
        private readonly CloudSyncService _cloud;
        private readonly GameNetworkFacade _network;
        private readonly WeeklyLeaderboardCoordinator _weekly;
        private readonly LeaderboardReadCache _cache;
        private readonly Dictionary<string, LeaderboardReadState> _boards = new();
        private readonly HashSet<string> _pending = new();
        private readonly SemaphoreSlim _slots = new(2);
        private readonly IDisposable _registration;
        private readonly string _environment = OnlineServicesCoordinator.EnvironmentName;
        private int _generation;
        private bool _menuActive;
        private bool _disposed;
        private bool _participationReady;
        private bool _journalReadFailed;

        public static LeaderboardReadService Instance { get; private set; }
        public event Action<string> ResultsChanged;

        public LeaderboardReadService(AccountService account, CloudSyncService cloud,
            GameNetworkFacade network, WeeklyLeaderboardCoordinator weekly)
        {
            _account = account;
            _cloud = cloud;
            _network = network;
            _weekly = weekly;
            _cache = LeaderboardReadCache.Load(_environment);
            Instance = this;
            _registration = OnlineServicesCoordinator.Register(RetryKey, WarmPendingAsync,
                () => !_disposed && _menuActive && _pending.Count > 0 && CanRead());
            _account.StateChanged += OnAccountChanged;
            _cloud.StatusChanged += OnCloudChanged;
            _network.NetworkModeChanged += OnConnectionChanged;
            OnlineServicesCoordinator.Resumed += OnConnectionChanged;
            GameDataManager.ProfileChanged += OnProfileChanged;
            _weekly.RunChanged += OnRunChanged;
        }

        /// <summary>Возвращает уже имеющиеся строки без ожидания сети; личная пометка проверяется каждый раз.</summary>
        public LeaderboardViewSnapshot GetSnapshot(string locationId, string partId)
        {
            var state = GetBoard(locationId, partId);
            var table = state.Table;
            var authorized = _account.TryGetAuthenticatedPlayerId(out var authenticatedId);
            var ownProfile = TryGetOwnPlayer(out _);
            var personalVisible = !AccountTransitionScope.IsActive &&
                !string.IsNullOrWhiteSpace(GameDataManager.OwnerPlayerId) &&
                table?.OwnerPlayerId == GameDataManager.OwnerPlayerId &&
                (!authorized || authenticatedId == GameDataManager.OwnerPlayerId);
            if (personalVisible != state.PersonalVisible)
            {
                state.PersonalVisible = personalVisible;
                state.ContentVersion++;
            }

            var status = _network.IsForcedOffline ? LeaderboardReadStatus.Offline :
                !CanRead() ? (_account.State == AccountState.Error
                    ? _account.RequiresReauthentication ? LeaderboardReadStatus.AuthenticationRequired :
                        _account.IsGuestRecoveryUnavailable ? LeaderboardReadStatus.ProfileRecoveryUnavailable : LeaderboardReadStatus.Unavailable
                    : LeaderboardReadStatus.Connecting) : state.Status;
            var participation = !authorized ? LeaderboardParticipationStatus.Connecting :
                !ownProfile ? LeaderboardParticipationStatus.ProfileRequired :
                _cloud.HasUnresolvedConflict ? LeaderboardParticipationStatus.Conflict :
                _account.TryGetLinkedPlayerId(out _) && !_cloud.IsInitialReconciliationComplete
                    ? LeaderboardParticipationStatus.Synchronizing : LeaderboardParticipationStatus.Ready;
            var resetPassed = DateTime.TryParse(table?.NextResetUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var reset) && DateTime.UtcNow >= reset.ToUniversalTime();
            return new LeaderboardViewSnapshot
            {
                LeaderboardId = state.BoardId, HasTable = table != null,
                IsRefreshing = state.Request != null, ContentVersion = state.ContentVersion,
                Top = table?.Top ?? Array.Empty<LeaderboardEntry>(),
                CurrentPlayer = personalVisible ? table.CurrentPlayer : null,
                VersionId = table?.VersionId, FetchedAtUtc = table?.FetchedAtUtc, NextResetUtc = table?.NextResetUtc,
                IsStale = !state.VerifiedThisSession || resetPassed || status != LeaderboardReadStatus.Ready,
                Status = status, ParticipationStatus = participation,
                PersonalStatus = authorized && !ownProfile ? LeaderboardPersonalStatus.ProfileRequired :
                    personalVisible ? state.PersonalStatus : LeaderboardPersonalStatus.Unavailable,
                LatestRun = ReadLatestRun(locationId, partId)
            };
        }

        /// <summary>Объединяет параллельные повторы одной таблицы, не скрывая её текущие данные.</summary>
        public Task RefreshAsync(string locationId, string partId)
        {
            if (_disposed) return Task.CompletedTask;
            var state = GetBoard(locationId, partId);
            return StartRefresh(state, personalOnly: false);
        }

        private Task StartRefresh(LeaderboardReadState state, bool personalOnly)
        {
            if (state.Request != null) return state.Request;
            if (Time.realtimeSinceStartupAsDouble - state.LastAttempt < 1) return Task.CompletedTask;
            if (!CanRead())
            {
                _pending.Add(state.BoardId);
                OnlineServicesCoordinator.StartUnityServices();
                OnlineServicesCoordinator.RequestRetry("account");
                Notify(state.BoardId);
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>();
            state.Request = completion.Task;
            state.LastAttempt = Time.realtimeSinceStartupAsDouble;
            _ = ReadAsync(state, completion, _generation, personalOnly);
            return completion.Task;
        }

        /// <summary>В меню заранее ставит доступные части дня в последовательную фоновую очередь.</summary>
        public void WarmMenuCache()
        {
            if (_disposed) return;
            _menuActive = true;
            foreach (var location in LevelSelectionModel.Create().Locations.Where(item => item.IsUnlocked))
                foreach (var part in location.Parts.Where(item => item.IsUnlocked))
                    _pending.Add(GetBoard(location.Id, part.Id).BoardId);
            OnlineServicesCoordinator.RequestRetry(RetryKey);
        }

        public void PauseMenuCache() => _menuActive = false;

        private async Task WarmPendingAsync()
        {
            var failed = false;
            foreach (var board in _pending.ToArray())
            {
                if (!_menuActive || _disposed || !CanRead()) return;
                var state = _boards[board];
                var personalOnly = state.Status == LeaderboardReadStatus.Ready && state.VerifiedThisSession &&
                    state.Table != null && state.PersonalStatus == LeaderboardPersonalStatus.Unavailable;
                await StartRefresh(state, personalOnly);
                if (state.Status == LeaderboardReadStatus.BoardUnavailable ||
                    state.Status == LeaderboardReadStatus.Ready &&
                    (!TryGetOwnPlayer(out _) || state.PersonalStatus != LeaderboardPersonalStatus.Unavailable))
                    _pending.Remove(board);
                else
                    failed = true;
            }
            if (failed) throw new InvalidOperationException("Leaderboard background refresh is pending.");
        }

        private LeaderboardReadState GetBoard(string locationId, string partId)
        {
            var board = LeaderboardService.ResolveLeaderboardId(locationId, partId);
            if (_boards.TryGetValue(board, out var state)) return state;
            var table = _cache.Tables.FirstOrDefault(item => item.LeaderboardId == board);
            // Старый профильный кеш служит fallback только для подтверждённого владельца.
            if (table == null && TryGetOwnPlayer(out _) && !_journalReadFailed)
            {
                try
                {
                    if (_weekly.TryGetCachedResults(locationId, partId, out var old)) table = old;
                }
                catch (Exception exception) { ReportJournalFailure(exception); }
            }
            state = new LeaderboardReadState
            {
                BoardId = board, LocationId = locationId, PartId = partId, Table = table,
                ContentVersion = 1, PersonalStatus = table?.Player == null
                    ? LeaderboardPersonalStatus.NoEntry : LeaderboardPersonalStatus.Ready
            };
            _boards.Add(board, state);
            return state;
        }

        private async Task ReadAsync(LeaderboardReadState state, TaskCompletionSource<bool> completion,
            int generation, bool personalOnly)
        {
            await _slots.WaitAsync();
            var playerId = _network.PlayerId;
            var profile = GameDataManager.ProfileId;
            var profileGeneration = GameDataManager.Generation;
            try
            {
                if (!IsCurrent(generation, playerId)) return;
                Notify(state.BoardId);
                // Повтор отдельного self-отказа сохраняет уже успешно прочитанный top.
                if (personalOnly && TryGetOwnPlayer(out var personalOwner) && personalOwner == playerId)
                {
                    var completed = await ReadPersonalAsync(state, state.Table, playerId, generation);
                    if (IsCurrent(generation, playerId)) FinishRefresh(state, completed);
                    return;
                }
                // Подтверждаем период с двух сторон чтения, один раз повторяем при reset.
                LeaderboardVersions season = null;
                IReadOnlyList<LeaderboardEntry> top = null;
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var before = await _network.GetSeasonAsync(state.BoardId);
                    top = await _network.GetTopEntriesAsync(state.BoardId);
                    season = await _network.GetSeasonAsync(state.BoardId);
                    if (!IsCurrent(generation, playerId)) return;
                    if (!string.IsNullOrWhiteSpace(season.VersionId) && before.VersionId == season.VersionId) break;
                    if (attempt == 1) throw new InvalidOperationException("Leaderboard period changed during refresh.");
                }

                var previous = state.Table;
                var table = new LeaderboardResultsSnapshot
                {
                    Environment = _environment, LeaderboardId = state.BoardId, VersionId = season.VersionId,
                    FetchedAtUtc = DateTime.UtcNow.ToString("o"), NextResetUtc = season.NextReset.ToUniversalTime().ToString("o"),
                    Entries = top.Select(LeaderboardCachedEntry.FromEntry).ToList()
                };
                if (previous?.VersionId == table.VersionId && previous.OwnerPlayerId == playerId)
                {
                    table.OwnerPlayerId = playerId;
                    table.Player = previous.Player;
                }
                ApplyTable(state, table);
                state.Status = LeaderboardReadStatus.Ready;
                if (previous?.VersionId != table.VersionId || previous.OwnerPlayerId != playerId)
                    state.PersonalStatus = LeaderboardPersonalStatus.Unavailable;
                state.VerifiedThisSession = true;
                SaveCache(state.BoardId);
                Notify(state.BoardId);

                // Публичный ответ уже виден; сохранение периода отправки остаётся условным и строгим.
                try { _weekly.TryRememberReadSeason(state.BoardId, season, playerId, profile, profileGeneration); }
                catch (Exception exception)
                {
                    DebugManager.DiagStability($"[Leaderboard] season save deferred board={state.BoardId} error={exception.GetType().Name}.");
                }
                var personalRead = !TryGetOwnPlayer(out var owner) || owner != playerId ||
                    await ReadPersonalAsync(state, table, playerId, generation);
                if (!IsCurrent(generation, playerId)) return;
                FinishRefresh(state, personalRead);
            }
            catch (Exception exception)
            {
                if (generation != _generation || _disposed) return;
                state.Status = Classify(exception);
                if (state.Status != LeaderboardReadStatus.BoardUnavailable) _pending.Add(state.BoardId);
                DebugManager.DiagStability($"[Leaderboard] read board={state.BoardId} status={state.Status} error={exception.GetType().Name}.");
            }
            finally
            {
                _slots.Release();
                state.Request = null;
                completion.TrySetResult(true);
                if (!_disposed)
                {
                    Notify(state.BoardId);
                    if (state.RefreshAfterRequest)
                    {
                        state.RefreshAfterRequest = false;
                        state.LastAttempt = double.NegativeInfinity;
                        _ = RefreshAsync(state.LocationId, state.PartId);
                    }
                }
            }
        }

        private async Task<bool> ReadPersonalAsync(LeaderboardReadState state, LeaderboardResultsSnapshot table,
            string playerId, int generation)
        {
            try
            {
                var player = await _network.GetPlayerEntryAsync(state.BoardId);
                var after = await _network.GetSeasonAsync(state.BoardId);
                if (!IsCurrent(generation, playerId) || !TryGetOwnPlayer(out var owner) || owner != playerId) return false;
                if (after.VersionId != table.VersionId)
                {
                    state.VerifiedThisSession = false;
                    state.PersonalStatus = LeaderboardPersonalStatus.Unavailable;
                    return false;
                }
                var entry = LeaderboardCachedEntry.FromEntry(player);
                if (table.OwnerPlayerId != playerId || !SameEntry(table.Player, entry)) state.ContentVersion++;
                table.OwnerPlayerId = playerId;
                table.Player = entry;
                state.PersonalStatus = player == null ? LeaderboardPersonalStatus.NoEntry : LeaderboardPersonalStatus.Ready;
                return true;
            }
            catch (Exception exception)
            {
                if (!IsCurrent(generation, playerId)) return false;
                state.PersonalStatus = LeaderboardPersonalStatus.Unavailable;
                DebugManager.DiagStability($"[Leaderboard] personal read deferred board={state.BoardId} error={exception.GetType().Name}.");
                return false;
            }
        }

        private void SaveCache(string board)
        {
            if (!_cache.TrySave(_environment))
                DebugManager.DiagStability($"[Leaderboard] cache save deferred board={board}.");
        }

        private void FinishRefresh(LeaderboardReadState state, bool personalRead)
        {
            SaveCache(state.BoardId);
            if (personalRead) _pending.Remove(state.BoardId);
            else _pending.Add(state.BoardId);
        }

        private WeeklyLeaderboardRun ReadLatestRun(string locationId, string partId)
        {
            if (_journalReadFailed) return null;
            try { return _weekly.GetLatestRun(locationId, partId); }
            catch (Exception exception) { ReportJournalFailure(exception); return null; }
        }

        private void ReportJournalFailure(Exception exception)
        {
            _journalReadFailed = true;
            DebugManager.DiagStability($"[Leaderboard] local run details unavailable ({exception.GetType().Name}).");
        }

        private void ApplyTable(LeaderboardReadState state, LeaderboardResultsSnapshot table)
        {
            var previous = state.Table;
            if (previous == null || previous.Entries.Count != table.Entries.Count ||
                previous.Entries.Where((entry, index) => !SameEntry(entry, table.Entries[index])).Any() ||
                previous.OwnerPlayerId != table.OwnerPlayerId || !SameEntry(previous.Player, table.Player))
                state.ContentVersion++;
            state.Table = table;
            _cache.Tables.RemoveAll(item => item.LeaderboardId == table.LeaderboardId);
            _cache.Tables.Add(table);
        }

        private static bool SameEntry(LeaderboardCachedEntry left, LeaderboardCachedEntry right) =>
            ReferenceEquals(left, right) || left != null && right != null && left.PlayerId == right.PlayerId &&
            left.PlayerName == right.PlayerName && left.Rank == right.Rank && left.Score == right.Score;

        private bool CanRead() => !_disposed && !_network.IsForcedOffline && OnlineServicesCoordinator.UnityServicesReady &&
            _account.TryGetAuthenticatedPlayerId(out _);

        private bool TryGetOwnPlayer(out string playerId) => _account.TryGetAuthenticatedPlayerId(out playerId) &&
            !AccountTransitionScope.IsActive && playerId == GameDataManager.OwnerPlayerId;

        private bool IsCurrent(int generation, string playerId) => !_disposed && generation == _generation &&
            CanRead() && _network.PlayerId == playerId;

        private LeaderboardReadStatus Classify(Exception exception)
        {
            if (exception is not LeaderboardsException leaderboard) return LeaderboardReadStatus.Unavailable;
            return leaderboard.Reason switch
            {
                LeaderboardsExceptionReason.NoInternetConnection => LeaderboardReadStatus.Offline,
                LeaderboardsExceptionReason.AccessTokenMissing or LeaderboardsExceptionReason.PlayerIdMissing or
                    LeaderboardsExceptionReason.Unauthorized => _account.RequiresReauthentication
                        ? LeaderboardReadStatus.AuthenticationRequired : LeaderboardReadStatus.Unavailable,
                LeaderboardsExceptionReason.LeaderboardNotFound or LeaderboardsExceptionReason.InvalidArgument or
                    LeaderboardsExceptionReason.ScoreSubmissionRequired => LeaderboardReadStatus.BoardUnavailable,
                _ => LeaderboardReadStatus.Unavailable
            };
        }

        private void OnAccountChanged(AccountState state) => OnProfileChanged();
        private void OnCloudChanged(CloudSyncStatusEnum status)
        {
            var ready = !_cloud.HasUnresolvedConflict && _cloud.IsInitialReconciliationComplete;
            NotifyAll();
            if (ready && !_participationReady && _menuActive) WarmMenuCache();
            _participationReady = ready;
        }

        private void OnProfileChanged()
        {
            _generation++;
            _journalReadFailed = false;
            foreach (var state in _boards.Values)
            {
                state.LastAttempt = double.NegativeInfinity;
                state.PersonalStatus = LeaderboardPersonalStatus.Unavailable;
                state.RefreshAfterRequest = state.Request != null;
            }
            NotifyAll();
            if (_menuActive) WarmMenuCache();
        }

        private void OnConnectionChanged()
        {
            NotifyAll();
            if (_menuActive) WarmMenuCache();
        }

        private void OnRunChanged(WeeklyLeaderboardRun run)
        {
            if (run == null || !_boards.TryGetValue(run.LeaderboardId, out var state)) return;
            Notify(state.BoardId);
            if (!_menuActive || run.Status == WeeklyRunStatus.Pending || run.Status == WeeklyRunStatus.AwaitingLocalSave) return;
            state.LastAttempt = double.NegativeInfinity;
            if (state.Request != null) state.RefreshAfterRequest = true;
            else _ = RefreshAsync(state.LocationId, state.PartId);
        }

        private void NotifyAll()
        {
            foreach (var board in _boards.Keys.ToArray()) Notify(board);
        }

        private void Notify(string board)
        {
            if (_disposed || ResultsChanged == null) return;
            foreach (Action<string> handler in ResultsChanged.GetInvocationList())
                try { handler(board); }
                catch (Exception exception)
                {
                    DebugManager.DiagStability($"[Leaderboard] view notification failed ({exception.GetType().Name}).");
                }
        }

        public void Dispose()
        {
            _disposed = true;
            _generation++;
            _registration.Dispose();
            _account.StateChanged -= OnAccountChanged;
            _cloud.StatusChanged -= OnCloudChanged;
            _network.NetworkModeChanged -= OnConnectionChanged;
            OnlineServicesCoordinator.Resumed -= OnConnectionChanged;
            GameDataManager.ProfileChanged -= OnProfileChanged;
            _weekly.RunChanged -= OnRunChanged;
            ResultsChanged = null;
            if (Instance == this) Instance = null;
        }
    }
}
