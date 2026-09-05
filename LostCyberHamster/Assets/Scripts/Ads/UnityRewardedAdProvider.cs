using System;
using System.Threading.Tasks;
using Assets.Scripts;
using UnityEngine.Advertisements;

namespace GameAds
{
    /// <summary>Адаптер установленного Advertisement Legacy SDK.</summary>
    public sealed partial class UnityRewardedAdProvider : IRewardedAdProvider
    {
        private TaskCompletionSource<bool> _initialization;
        private int _loadVersion;
        private int _consumedLoadVersion;
        public bool IsSupported => Advertisement.isSupported;
        public bool IsInitialized => Advertisement.isInitialized;
        public bool HasLoadedAd { get; private set; }
#if UNITY_IOS
        private const string Placement = "Rewarded_iOS";
        private static string GameId => Consts.IOS_GAME_ID;
#else
        private const string Placement = "Rewarded_Android";
        private static string GameId => Consts.ANDROID_GAME_ID;
#endif

        public Task InitializeAsync()
        {
            if (IsInitialized)
                return Task.CompletedTask;
            if (!IsSupported)
                throw new NotSupportedException("Unity Ads is unavailable on this platform.");
            if (_initialization != null && !_initialization.Task.IsCompleted)
                return _initialization.Task;

            // Незавершённую native initialization повторно не запускаем.
            _initialization = new TaskCompletionSource<bool>();
            try
            {
                Advertisement.Initialize(GameId, true,
                    new AdsInitializationListener(_initialization));
            }
            catch (Exception exception)
            {
                _initialization.TrySetException(exception);
            }
            return _initialization.Task;
        }

        public void Load(Action loaded, Action<string> failed)
        {
            if (HasLoadedAd)
            {
                loaded();
                return;
            }
            int loadVersion = ++_loadVersion;
            Advertisement.Load(Placement, new AdLoadListener(Placement, () =>
            {
                if (loadVersion > _consumedLoadVersion)
                    HasLoadedAd = true;
                loaded();
            }, failed));
        }

        public void Show(Action started, Action<bool> completed, Action<string> failed)
        {
            HasLoadedAd = false;
            _consumedLoadVersion = _loadVersion;
            Advertisement.Show(Placement, new AdShowListener(Placement, started, completed, failed));
        }
    }
}
