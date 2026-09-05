using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Online;
using Assets.Scripts.Account;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vues.GameCore;

namespace GameAds
{
    /// <summary>Владеет показом, его контекстом и локальным подтверждением награды.</summary>
    public sealed class RewardedAdService
    {
        private const string JournalFeature = "rewarded-ad";
        private const string OnlineJob = "rewarded-ads";
        private const double PreparationSeconds = 15;
        private static RewardedAdService _instance;
        private readonly IRewardedAdProvider _provider;
        private readonly IDisposable _registration;
        private RewardedAdRequest _active;
        private string _recoveredProfile;
        private double _recoverRetryAt;
        private NetworkReachability _lastReachability;
        private bool _lastLoaded;
        private bool _lastAccountTransition;
        private bool _lastProfileBlocked;
        private string _lastStatus = string.Empty;
        public event Action Changed;

        public static RewardedAdService Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                _instance = new RewardedAdService(new UnityRewardedAdProvider());
                var host = new GameObject(nameof(RewardedAdLifecycle));
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<RewardedAdLifecycle>().Service = _instance;
                return _instance;
            }
        }

        private RewardedAdService(IRewardedAdProvider provider)
        {
            _provider = provider;
            _lastReachability = Application.internetReachability;
            _registration = OnlineServicesCoordinator.Register(OnlineJob, InitializeAsync,
                () => _provider.IsSupported && !_provider.IsInitialized);
        }

        public bool IsBusy => _active != null;
        public bool CanRequest => !IsBusy && CanPersistRewards &&
            !GameDataManager.IsProfileReplacementBlocked && !AccountTransitionScope.IsActive &&
            _recoveredProfile == CurrentProfileKey && _provider.IsSupported &&
            (_provider.HasLoadedAd || Application.internetReachability != NetworkReachability.NotReachable);
        public RewardedAdRequest ActiveRequest => _active;
        private static string CurrentProfileKey => GameDataManager.ProfileId + ":" + GameDataManager.Generation;
        private static bool CanPersistRewards
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
                    return false;
#endif
                return GameDataManager.IsLoaded;
            }
        }
        public string StatusKey
        {
            get
            {
                if (_active != null)
                {
                    switch (_active.State)
                    {
                        case RewardedAdState.Preparing:
                        case RewardedAdState.Loading: return "ads_loading";
                        case RewardedAdState.Settling: return "ads_saving_reward";
                        default: return "ads_waiting_result";
                    }
                }
                if (!_provider.IsSupported)
                    return "ads_unavailable";
                if (CanPersistRewards && _recoveredProfile != CurrentProfileKey)
                    return "ads_saving_reward";
                if (!_provider.HasLoadedAd && Application.internetReachability == NetworkReachability.NotReachable)
                    return "ads_offline";
                return _lastStatus;
            }
        }

        public void RequestInitialization() => OnlineServicesCoordinator.RequestRetry(OnlineJob);

        private async Task InitializeAsync()
        {
            try { await _provider.InitializeAsync(); }
            finally { Notify(); }
        }

        public RewardedAdRequest RequestShop(ShopItem item)
        {
            if (item == null || item.resource != ResourceType.Advertisement || item.amount <= 0 ||
                (item.type != ResourceType.Coins && item.type != ResourceType.Crystals))
                return null;
            return Begin(new RewardedAdIntent
            {
                RewardType = item.type,
                RewardAmount = item.amount,
                ShopItemId = item.id,
                SceneHandle = SceneManager.GetActiveScene().handle
            }, null, null);
        }

        public RewardedAdRequest RequestRevive(string runId, int sceneHandle,
            Func<bool> canRevive, Action revive)
        {
            return Begin(new RewardedAdIntent
            {
                IsRevive = true,
                RunId = runId,
                SceneHandle = sceneHandle
            }, canRevive, revive);
        }

        private RewardedAdRequest Begin(RewardedAdIntent intent, Func<bool> canRevive, Action revive)
        {
            Recover();
            if (!CanRequest)
                return null;

            // Фиксируем владельца и обещанную награду до асинхронных callbacks.
            intent.RequestId = Guid.NewGuid().ToString("N");
            intent.ProfileId = GameDataManager.ProfileId;
            intent.OwnerPlayerId = GameDataManager.OwnerPlayerId;
            intent.Generation = GameDataManager.Generation;
            _active = new RewardedAdRequest
            {
                Intent = intent,
                State = RewardedAdState.Preparing,
                StartedAt = Time.realtimeSinceStartupAsDouble,
                CanRevive = canRevive,
                Revive = revive
            };
            _lastStatus = string.Empty;
            DebugManager.DiagStability($"[ADS] {intent.RequestId} Preparing.");
            RequestInitialization();
            Notify();
            return _active;
        }

        /// <summary>Отменяет подготовку; после Show отсоединяет только игровой контекст.</summary>
        public void CancelContext(RewardedAdRequest request)
        {
            if (request == null || request.IsFinished)
                return;
            request.ContextCancelled = true;
            request.Revive = null;
            request.CanRevive = null;
            if (request.State == RewardedAdState.Preparing || request.State == RewardedAdState.Loading)
                Finish(request, RewardedAdState.Cancelled, string.Empty);
        }

        internal void SceneUnloaded(int sceneHandle)
        {
            if (_active != null && _active.Intent.SceneHandle == sceneHandle)
            {
                if (_active.Intent.IsRevive || _active.State == RewardedAdState.Preparing ||
                    _active.State == RewardedAdState.Loading)
                    CancelContext(_active);
            }
        }

        internal void Tick(bool foreground, double delta)
        {
            Recover();
            if (_lastReachability != Application.internetReachability || _lastLoaded != _provider.HasLoadedAd ||
                _lastAccountTransition != AccountTransitionScope.IsActive ||
                _lastProfileBlocked != GameDataManager.IsProfileReplacementBlocked)
            {
                _lastReachability = Application.internetReachability;
                _lastLoaded = _provider.HasLoadedAd;
                _lastAccountTransition = AccountTransitionScope.IsActive;
                _lastProfileBlocked = GameDataManager.IsProfileReplacementBlocked;
                Notify();
            }
            var request = _active;
            if (request == null)
                return;
            double now = Time.realtimeSinceStartupAsDouble;

            // До Show ограничиваем ожидание; поздний Load отменённой операции безопасен.
            if (request.State == RewardedAdState.Preparing || request.State == RewardedAdState.Loading)
            {
                if (!IsCurrentOwner(request) || now - request.StartedAt >= PreparationSeconds)
                {
                    Finish(request, RewardedAdState.Failed, "ads_try_again");
                    return;
                }
                if (foreground && request.State == RewardedAdState.Preparing && _provider.IsInitialized)
                {
                    SetState(request, RewardedAdState.Loading);
                    try { _provider.Load(() => Loaded(request), error => Failed(request, error)); }
                    catch (Exception exception) { Failed(request, exception.GetType().Name); }
                }
                if (foreground && request.State == RewardedAdState.Loading && request.LoadCompleted)
                    SubmitShow(request);
                return;
            }

            // После Show watchdog меняет сообщение, но сохраняет право на COMPLETED.
            if (request.IsNativePending && foreground)
            {
                request.ForegroundSeconds += Math.Max(0, delta);
                if ((request.State == RewardedAdState.ShowSubmitted && now - request.SubmittedAt >= 10) ||
                    request.ForegroundSeconds >= 120)
                    SetState(request, RewardedAdState.AwaitingResult);
            }
            if (foreground && request.State == RewardedAdState.Settling && now >= request.RetrySettlementAt)
                Settle(request);
        }

        private void Loaded(RewardedAdRequest request)
        {
            if (_active == request && request.State == RewardedAdState.Loading)
                request.LoadCompleted = true;
        }

        private void SubmitShow(RewardedAdRequest request)
        {
            if (_active != request || request.State != RewardedAdState.Loading)
                return;
            if (request.ContextCancelled || !IsCurrentOwner(request) || AccountTransitionScope.IsActive ||
                GameDataManager.IsProfileReplacementBlocked ||
                Time.realtimeSinceStartupAsDouble - request.StartedAt >= PreparationSeconds)
            {
                Finish(request, RewardedAdState.Cancelled, string.Empty);
                return;
            }

            // Durable intent и блокировка замены профиля предшествуют native Show.
            try
            {
                request.ProfileBlock = GameDataManager.AcquireProfileReplacementBlock();
                GameDataManager.ExecuteTechnicalTransaction(() => WriteIntent(request.Intent));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Ads] Cannot persist intent: {exception.GetType().Name}.");
                Finish(request, RewardedAdState.Failed, "shop_save_failed");
                return;
            }
            request.SubmittedAt = Time.realtimeSinceStartupAsDouble;
            SetState(request, RewardedAdState.ShowSubmitted);
            try
            {
                _provider.Show(() => Started(request), completed => Completed(request, completed),
                    error => Failed(request, error));
            }
            catch (Exception exception)
            {
                // Show мог уже дойти до native слоя: ждём поздний результат, не запускаем второй.
                Debug.LogWarning($"[Ads] Show outcome unknown: {exception.GetType().Name}.");
                SetState(request, RewardedAdState.AwaitingResult);
            }
        }

        private void Started(RewardedAdRequest request)
        {
            if (_active == request && !request.TerminalReceived && request.IsNativePending)
                SetState(request, RewardedAdState.Showing);
        }

        private void Failed(RewardedAdRequest request, string error)
        {
            if (_active != request || request.TerminalReceived || request.IsFinished)
                return;
            request.TerminalReceived = true;
            DebugManager.DiagStability($"[ADS] {request.RequestId} SDK failure: {error}.");
            Finish(request, RewardedAdState.Failed, "ads_try_again");
        }

        private void Completed(RewardedAdRequest request, bool completed)
        {
            if (_active != request || request.TerminalReceived || request.IsFinished)
                return;
            request.TerminalReceived = true;
            if (!completed)
            {
                Finish(request, RewardedAdState.Skipped, "ads_not_completed");
                return;
            }

            // Подтверждение SDK не зависит от текущего соединения или видимого экрана.
            request.CompletionAccepted = true;
            request.Intent.CompletionReceived = true;
            SetState(request, RewardedAdState.Settling);
            Settle(request);
        }

        private void Settle(RewardedAdRequest request)
        {
            if (_active != request || !request.CompletionAccepted || !IsCurrentOwner(request))
                return;
            request.RetrySettlementAt = Time.realtimeSinceStartupAsDouble + 5;
            try
            {
                // Сохраняем completion до выдачи, чтобы восстановить её после сбоя второй записи.
                GameDataManager.ExecuteTechnicalTransaction(() => WriteIntent(request.Intent));
                bool newlyGranted = false;
                GameDataManager.ExecuteTransaction(CheckpointReason.RewardedAdRewardGranted, () =>
                {
                    var player = GameDataManager.PlayerData;
                    player.AppliedRewardedRequestIds ??= new List<string>();
                    if (!player.AppliedRewardedRequestIds.Contains(request.RequestId))
                    {
                        if (!request.Intent.IsRevive)
                            ApplyShopReward(request.Intent);
                        player.AppliedRewardedRequestIds.Add(request.RequestId);
                        newlyGranted = true;
                    }
                    GameDataManager.SetJournalJson(JournalFeature, string.Empty);
                });

                // UI и игровой эффект видят только уже сохранённую выдачу.
                if (newlyGranted && !request.Intent.IsRevive)
                {
                    ResourceManager.NotifyBalancesChangedAfterCommit();
                    try { GameEventsManager.ItemBought(request.Intent.ShopItemId, ResourceType.Advertisement, 0); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                if (newlyGranted && request.Intent.IsRevive && !request.ContextCancelled &&
                    request.CanRevive?.Invoke() == true)
                {
                    try { request.Revive?.Invoke(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                Finish(request, RewardedAdState.Completed, "ads_reward_granted");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Ads] Reward awaits local save: {exception.GetType().Name}.");
                SetState(request, RewardedAdState.Settling);
            }
        }

        private static void ApplyShopReward(RewardedAdIntent intent)
        {
            if (!ResourceManager.AddResource(intent.RewardType, intent.RewardAmount, notify: false))
                throw new InvalidOperationException("Reward could not be added to the balance.");
        }

        private void Recover()
        {
            if (!CanPersistRewards || _active != null)
                return;
            string profile = CurrentProfileKey;
            if (_recoveredProfile == profile)
                return;
            if (Time.realtimeSinceStartupAsDouble < _recoverRetryAt)
                return;
            try
            {
                string json = GameDataManager.GetJournalJson(JournalFeature);
                if (string.IsNullOrEmpty(json))
                {
                    _recoveredProfile = profile;
                    Notify();
                    return;
                }
                var intent = JsonUtility.FromJson<RewardedAdIntent>(json);
                if (intent == null || intent.ProfileId != GameDataManager.ProfileId ||
                    (intent.OwnerPlayerId != null && intent.OwnerPlayerId != GameDataManager.OwnerPlayerId) ||
                    !intent.CompletionReceived)
                {
                    GameDataManager.ExecuteTechnicalTransaction(() =>
                        GameDataManager.SetJournalJson(JournalFeature, string.Empty));
                    _recoveredProfile = profile;
                    Notify();
                    return;
                }

                // Погибшая попытка не оживает; подтверждённая награда магазина восстанавливается.
                // Первый owner bind переносит журнал того же ProfileId из локального слота.
                intent.OwnerPlayerId = GameDataManager.OwnerPlayerId;
                intent.Generation = GameDataManager.Generation;
                _active = new RewardedAdRequest
                {
                    Intent = intent,
                    State = RewardedAdState.Settling,
                    CompletionAccepted = true,
                    TerminalReceived = true,
                    ContextCancelled = true,
                    ProfileBlock = GameDataManager.AcquireProfileReplacementBlock()
                };
                _recoveredProfile = profile;
                Notify();
            }
            catch (Exception exception)
            {
                _recoverRetryAt = Time.realtimeSinceStartupAsDouble + 5;
                Notify();
                Debug.LogWarning($"[Ads] Reward recovery deferred: {exception.GetType().Name}.");
            }
        }

        private static bool IsCurrentOwner(RewardedAdRequest request) =>
            request.Intent.ProfileId == GameDataManager.ProfileId &&
            request.Intent.OwnerPlayerId == GameDataManager.OwnerPlayerId &&
            request.Intent.Generation == GameDataManager.Generation;

        private static void WriteIntent(RewardedAdIntent intent) =>
            GameDataManager.SetJournalJson(JournalFeature, JsonUtility.ToJson(intent));

        private void Finish(RewardedAdRequest request, RewardedAdState state, string status)
        {
            if (_active != request)
                return;
            request.State = state;
            DebugManager.DiagStability($"[ADS] {request.RequestId} {state}.");
            request.ProfileBlock?.Dispose();
            request.ProfileBlock = null;
            request.Revive = null;
            request.CanRevive = null;
            _active = null;
            _lastStatus = status;
            request.Notify();
            Notify();
        }

        private void SetState(RewardedAdRequest request, RewardedAdState state)
        {
            if (request.State == state)
                return;
            request.State = state;
            DebugManager.DiagStability($"[ADS] {request.RequestId} {state}.");
            request.Notify();
            Notify();
        }

        private void Notify()
        {
            var handlers = Changed;
            if (handlers == null)
                return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        internal void Shutdown()
        {
            _registration.Dispose();
            _active?.ProfileBlock?.Dispose();
            _active = null;
            Changed = null;
            if (_instance == this)
                _instance = null;
        }
    }
}
