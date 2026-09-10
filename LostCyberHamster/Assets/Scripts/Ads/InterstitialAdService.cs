using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Online;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameAds
{
    /// <summary>Выбирает рекламный сценарий один раз, затем показывает готовое видео после очереди результатов.</summary>
    public sealed class InterstitialAdService
    {
        private static InterstitialAdService _instance;
        public static InterstitialAdService Instance => _instance ??= new InterstitialAdService();
        private readonly UnityInterstitialAdProvider _provider = new();
        private string _profile;
        private long _generation;
        private DateTime? _backgroundAt;
        private bool _foreground = true;
        private int _wins;
        private double _activeSeconds;
        private double _nextLoadAt;
        private string _selectedWin;
        private bool _selectedPause;
        private bool _selectedAsPause;
        public bool AllowsWinBonus(string winId) => _profile != GameDataManager.ProfileId ||
            _generation != GameDataManager.Generation || _selectedWin != winId || !_selectedAsPause;
        private int _scene;
        public bool IsBusy { get; private set; }
        public bool HasSelectedPause => _selectedPause && _profile == GameDataManager.ProfileId &&
            _generation == GameDataManager.Generation && _scene == SceneManager.GetActiveScene().handle;
        private bool CanUse => GameDataManager.IsLoaded && !TutorialStorage.IsPlayerDataBackupActive &&
            !GameDataManager.HasProgressionTestingBackup &&
            !AutomationRuntimePrefs.IsTestLevelAutomationRun() && !AccountTransitionScope.IsActive;

        public void Tick(bool foreground)
        {
            if (!CanUse || IsBusy) return;
            bool rewarded = RewardedAdService.Instance.IsBusy;
            if (!foreground && _foreground && !rewarded) _backgroundAt = DateTime.UtcNow;
            bool nextVisit = foreground && _backgroundAt.HasValue &&
                DateTime.UtcNow - _backgroundAt.Value >= TimeSpan.FromMinutes(30);
            if (foreground) _backgroundAt = null;
            _foreground = foreground;
            if (!foreground || rewarded || GameDataManager.IsProfileReplacementBlocked) return;
            if (_profile != GameDataManager.ProfileId || _generation != GameDataManager.Generation || nextVisit)
            {
                try
                {
                    bool committed = false;
                    GameDataManager.ExecuteTransaction(CheckpointReason.MonetizationVisitStarted, () =>
                    {
                        var state = GameDataManager.PlayerData.Monetization ??= new MonetizationState();
                        state.VisitCount = Math.Min(int.MaxValue - 1, state.VisitCount) + 1;
                        if (!state.InterstitialVariantAssigned && MonetizationConfig.Current.EnableInterstitial)
                        {
                            // Назначение сохраняется до первого показа; изменения процента его не меняют.
                            state.InterstitialVariant = UnityEngine.Random.Range(0, 100) < MonetizationConfig.Current.InterstitialVariantPercent;
                            state.InterstitialVariantAssigned = true;
                        }
                    }, () => committed = true);
                    if (!committed) return;
                    _profile = GameDataManager.ProfileId;
                    _generation = GameDataManager.Generation;
                    ResetInterval();
                    _selectedWin = null;
                    _selectedPause = false;
                }
                catch (Exception exception) { Debug.LogWarning($"[Ads] Visit save deferred: {exception.GetType().Name}."); return; }
            }
            var data = GameDataManager.PlayerData.Monetization;
            if (!MonetizationConfig.Current.EnableInterstitial || data?.InterstitialVariant != true ||
                data.NoAds || data.VisitCount < 2 || GameNetworkFacade.Instance.IsForcedOffline ||
                Application.internetReachability == NetworkReachability.NotReachable ||
                Time.realtimeSinceStartupAsDouble < _nextLoadAt) return;
            _nextLoadAt = Time.realtimeSinceStartupAsDouble + 30;
            try { _provider.Load(); }
            catch (Exception exception) { Debug.LogWarning($"[Ads] Interstitial load failed: {exception.GetType().Name}."); }
        }

        public void RecordActiveGameplay(double seconds)
        {
            if (CanUse && _foreground && !IsBusy && !RewardedAdService.Instance.IsBusy &&
                _profile == GameDataManager.ProfileId && _generation == GameDataManager.Generation &&
                seconds > 0 && seconds < 1) _activeSeconds += seconds;
        }

        public void SelectWin(string winId)
        {
            if (string.IsNullOrEmpty(winId) || _selectedWin == winId ||
                GameDataManager.PlayerData?.Monetization?.LastWinId != winId) return;
            Tick(true);
            _selectedWin = winId;
            _scene = SceneManager.GetActiveScene().handle;
            _wins = Math.Min(int.MaxValue - 1, _wins) + 1;
            _selectedPause = MonetizationConfig.Current.EnableInterstitial && _provider.IsReady &&
                InterstitialPolicy.IsEligible(GameDataManager.PlayerData.Monetization, DateTime.UtcNow, _wins, _activeSeconds);
            _selectedAsPause = _selectedPause;
        }

        public void ResetInterval() { _activeSeconds = 0; _wins = 0; }

        public async Task ShowSelectedAfterResultsAsync(Func<bool> isCurrent)
        {
            if (!HasSelectedPause) return;
            _selectedPause = false;
            if (!CanUse || isCurrent?.Invoke() != true || !MonetizationConfig.Current.EnableInterstitial ||
                !_provider.IsReady || RewardedAdService.Instance.IsBusy || GameDataManager.IsProfileReplacementBlocked ||
                !InterstitialPolicy.IsEligible(GameDataManager.PlayerData.Monetization, DateTime.UtcNow, _wins, _activeSeconds)) return;
            IsBusy = true;
            using var profileBlock = GameDataManager.AcquireProfileReplacementBlock();
            using var inputBlock = UiInputBlock.Acquire();
            bool started = false;
            bool reserved = false;
            var day = DateTime.UtcNow.Date.Ticks;
            try
            {
                // Durable резерв до native Show сохраняет максимум при неизвестном результате или остановке процесса.
                GameDataManager.ExecuteTransaction(CheckpointReason.InterstitialReserved, () =>
                {
                    var state = GameDataManager.PlayerData.Monetization;
                    if (state.InterstitialDayUtcTicks != day) state.InterstitialDayCount = 0;
                    state.InterstitialDayUtcTicks = day;
                    state.InterstitialDayCount++;
                }, () => reserved = true);
                if (!reserved) return;
                var closed = new TaskCompletionSource<bool>();
                try { _provider.Show(() => { started = true; MonetizationEvent.Record("show", "interstitial", _selectedWin); }, shown => closed.TrySetResult(shown)); }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Ads] Native show outcome unknown: {exception.GetType().Name}.");
                }
                bool shown = await closed.Task;
                MonetizationEvent.Record(shown || started ? "closed" : "failed", "interstitial", _selectedWin);
                if (!shown && !started)
                    GameDataManager.ExecuteTransaction(CheckpointReason.InterstitialReserved, () =>
                    {
                        var state = GameDataManager.PlayerData.Monetization;
                        if (state.InterstitialDayUtcTicks == day) state.InterstitialDayCount = Math.Max(0, state.InterstitialDayCount - 1);
                    });
                else ResetInterval();
            }
            catch (Exception exception)
            {
                // Неизвестный исход не разрешает второй автоматический показ в тот же день.
                Debug.LogWarning($"[Ads] Interstitial transition: {exception.GetType().Name}.");
                if (reserved) ResetInterval();
            }
            finally
            {
                while (!Application.isFocused || RewardedAdInputGuard.HasActiveInput()) await Task.Yield();
                await Task.Yield();
                await Task.Yield();
                IsBusy = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _instance = null;
    }
}
