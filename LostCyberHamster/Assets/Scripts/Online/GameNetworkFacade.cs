using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameAds;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using GameManagement.Leaderboard;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Online
{
    /// <summary>Объединяет обращения игры к SDK и DEV-запрет начала сетевых операций.</summary>
    public sealed class GameNetworkFacade : IAccountAuthenticationGateway, IUnityPlayerAccountGateway,
        IAccountSessionStatus, IAccountProfileGateway, ICloudSaveGateway, IRewardedAdProvider
    {
        private static GameNetworkFacade _instance;
        private readonly UnityAccountAuthenticationGateway _authentication = new();
        private readonly UnityPlayerAccountGateway _playerAccount = new();
        private readonly UnityCloudSaveGateway _cloud = new();
        private readonly LeaderboardService _leaderboard = new();
        private readonly UnityRewardedAdProvider _ads = new();
        private readonly HashSet<string> _reportedBlocks = new(StringComparer.Ordinal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string ForcedOfflineKey = "DevTools.Networking.ForcedOffline";
#endif

        private GameNetworkFacade()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            IsForcedOffline = PlayerPrefs.GetInt(ForcedOfflineKey, 0) == 1;
#endif
        }

        public static GameNetworkFacade Instance => _instance ??= new GameNetworkFacade();
        public bool IsForcedOffline { get; private set; }
        public event Action NetworkModeChanged;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Сохраняет DEV-режим и возобновляет существующие фоновые повторы при включении.</summary>
        public void SetForcedOffline(bool forcedOffline)
        {
            if (IsForcedOffline == forcedOffline) return;
            PlayerPrefs.SetInt(ForcedOfflineKey, forcedOffline ? 1 : 0);
            PlayerPrefs.Save();
            IsForcedOffline = forcedOffline;
            _reportedBlocks.Clear();
            DebugManager.DiagStability($"[NETWORK] forcedOffline={forcedOffline}.");

            // Ошибка одного UI-подписчика не блокирует остальные сервисы и восстановление сети.
            var handlers = NetworkModeChanged;
            if (handlers != null)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try { handler(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
            if (!forcedOffline) OnlineServicesCoordinator.Resume();
        }
#endif

        /// <summary>Инициализирует UGS после разрешения сетевого старта.</summary>
        public async Task InitializeUnityServicesAsync()
        {
            if (OnlineServicesCoordinator.UnityServicesReady) return;
            EnsureNetworkAllowed(nameof(InitializeUnityServicesAsync));
            await UnityServices.InitializeAsync(new InitializationOptions()
                .SetEnvironmentName(OnlineServicesCoordinator.EnvironmentName));
        }

        // Статусы и локальные credentials остаются фактическими при любом DEV-режиме.
        public bool SessionTokenExists => _authentication.SessionTokenExists;
        public bool IsUnityPlayerAccountLinked => _authentication.IsUnityPlayerAccountLinked;
        public bool IsSignedIn => _authentication.IsSignedIn;
        bool IUnityPlayerAccountGateway.IsSignedIn => _playerAccount.IsSignedIn;
        public bool IsAuthorized => _authentication.IsAuthorized;
        public string PlayerId => _authentication.PlayerId;
        public string PlayerName => _authentication.PlayerName;
        public string Profile => _authentication.Profile;
        public event Action SessionExpired
        {
            add => _authentication.SessionExpired += value;
            remove => _authentication.SessionExpired -= value;
        }
        public void SwitchProfile(string profile) => _authentication.SwitchProfile(profile);
        public void SignOutPreservingCredentials() => _authentication.SignOutPreservingCredentials();
        public void SignOutAndClearLocalCredentials() => _authentication.SignOutAndClearLocalCredentials();
        public void SignOut() => _playerAccount.SignOut();

        /// <summary>Входит в гостевую сессию и отдельно получает отсутствующее имя.</summary>
        public async Task SignInAnonymouslyAsync(bool createAccount)
        {
            EnsureNetworkAllowed(nameof(SignInAnonymouslyAsync));
            await _authentication.SignInAnonymouslyAsync(createAccount);
            await EnsurePlayerNameAsync();
        }

        /// <summary>Привязывает аккаунт, сохраняя существующий контракт результата отказа.</summary>
        public async Task<AccountLinkResult> LinkWithUnityAsync(string accessToken)
        {
            if (!CanStartNetworkOperation(nameof(LinkWithUnityAsync))) return AccountLinkResult.Failed;
            var result = await _authentication.LinkWithUnityAsync(accessToken);
            if (result == AccountLinkResult.Linked) await EnsurePlayerNameAsync();
            return result;
        }

        /// <summary>Обновляет имя через разрешённое сетевое обращение.</summary>
        public async Task<string> UpdatePlayerNameAsync(string playerName)
        {
            EnsureNetworkAllowed(nameof(UpdatePlayerNameAsync));
            return await _authentication.UpdatePlayerNameAsync(playerName);
        }

        /// <summary>Входит в существующий аккаунт и отдельно получает отсутствующее имя.</summary>
        public async Task SignInWithUnityAsync(string accessToken)
        {
            EnsureNetworkAllowed(nameof(SignInWithUnityAsync));
            await _authentication.SignInWithUnityAsync(accessToken);
            await EnsurePlayerNameAsync();
        }

        /// <summary>Разрешает начало Unity Player Account browser flow.</summary>
        public async Task<string> SignInAsync()
        {
            EnsureNetworkAllowed(nameof(SignInAsync));
            return await _playerAccount.SignInAsync();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Удаляет связь тестового аккаунта при разрешённом доступе.</summary>
        public async Task UnlinkUnityAsync()
        {
            EnsureNetworkAllowed(nameof(UnlinkUnityAsync));
            await _authentication.UnlinkUnityAsync();
        }
#endif

        /// <summary>Ошибка дополнительного имени сохраняет успешно установленную сессию.</summary>
        private async Task EnsurePlayerNameAsync()
        {
            if (!string.IsNullOrWhiteSpace(_authentication.PlayerName)) return;
            try
            {
                EnsureNetworkAllowed("GetPlayerNameAsync");
                await _authentication.GetPlayerNameAsync();
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[NETWORK] player name deferred: {exception.GetType().Name}.");
            }
        }

        /// <summary>Читает облачный снимок через общий запрет сетевых обращений.</summary>
        public async Task<CloudSaveReadResult> LoadSnapshotAsync()
        {
            EnsureNetworkAllowed(nameof(LoadSnapshotAsync));
            return await _cloud.LoadSnapshotAsync();
        }

        /// <summary>Отправляет снимок с исходным write lock; поздний ACK остаётся действительным.</summary>
        public async Task<CloudSaveVersion> SaveSnapshotAsync(CloudSaveSnapshot snapshot, string expectedServerRevision)
        {
            EnsureNetworkAllowed(nameof(SaveSnapshotAsync));
            return await _cloud.SaveSnapshotAsync(snapshot, expectedServerRevision);
        }

        /// <summary>Получает текущую серверную неделю.</summary>
        public async Task<LeaderboardVersions> GetSeasonAsync(string leaderboardId)
        {
            EnsureNetworkAllowed(nameof(GetSeasonAsync));
            return await _leaderboard.GetSeasonAsync(leaderboardId);
        }

        /// <summary>Читает собственный рекорд вместе с метаданными подтверждения.</summary>
        public async Task<LeaderboardEntry> GetPlayerEntryAsync(string leaderboardId)
        {
            EnsureNetworkAllowed(nameof(GetPlayerEntryAsync));
            return await _leaderboard.GetPlayerEntryAsync(leaderboardId);
        }

        /// <summary>Отправляет рекорд строго в исходный серверный период.</summary>
        public async Task<LeaderboardEntry> SubmitVersionedScoreAsync(
            string leaderboardId, int score, string versionId, WeeklyScoreMetadata metadata)
        {
            EnsureNetworkAllowed(nameof(SubmitVersionedScoreAsync));
            return await _leaderboard.SubmitVersionedScoreAsync(leaderboardId, score, versionId, metadata);
        }

        /// <summary>Читает общие строки независимо от личной записи.</summary>
        public async Task<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(string leaderboardId)
        {
            EnsureNetworkAllowed(nameof(GetTopEntriesAsync));
            return await _leaderboard.GetTopEntriesAsync(leaderboardId);
        }

        /// <summary>Получает строгий полный ответ для существующих потребителей личного рекорда.</summary>
        public async Task<(IReadOnlyList<LeaderboardEntry> Top, LeaderboardEntry CurrentPlayer)> GetResultsAsync(
            string locationId, string partOfDayId)
        {
            var board = LeaderboardService.ResolveLeaderboardId(locationId, partOfDayId);
            EnsureNetworkAllowed("GetTopEntriesAsync");
            var top = await _leaderboard.GetTopEntriesAsync(board);
            var player = await GetPlayerEntryAsync(board);
            return (top, player);
        }

        public bool IsSupported => _ads.IsSupported;
        public bool IsInitialized => _ads.IsInitialized;
        public bool HasLoadedAd => _ads.HasLoadedAd;

        /// <summary>Инициализирует рекламу; уже готовый SDK доступен офлайн.</summary>
        public async Task InitializeAsync()
        {
            if (_ads.IsInitialized) return;
            EnsureNetworkAllowed("InitializeAdsAsync");
            await _ads.InitializeAsync();
        }

        /// <summary>Получает загруженный ролик из кэша или разрешает новую загрузку.</summary>
        public void Load(Action loaded, Action<string> failed)
        {
            if (!_ads.HasLoadedAd && !CanStartNetworkOperation("LoadAd"))
            {
                failed?.Invoke("ads_offline");
                return;
            }
            _ads.Load(loaded, failed);
        }

        /// <summary>Передаёт готовый ролик SDK; результат показа принимается при любом режиме.</summary>
        public void Show(Action started, Action<bool> completed, Action<string> failed)
        {
            if (!_ads.HasLoadedAd && !CanStartNetworkOperation("ShowAd"))
            {
                failed?.Invoke("ads_offline");
                return;
            }
            _ads.Show(started, completed, failed);
        }

        private void EnsureNetworkAllowed(string operation)
        {
            if (!CanStartNetworkOperation(operation))
                throw new IOException("Симуляция офлайна включена. Включите доступ в DEV / Networking.");
        }

        private bool CanStartNetworkOperation(string operation)
        {
            if (!IsForcedOffline) return true;
            if (_reportedBlocks.Add(operation))
                DebugManager.DiagStability($"[NETWORK] blocked={operation}; forcedOffline=true.");
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (_instance != null) _instance.NetworkModeChanged = null;
            _instance = null;
        }
    }
}
