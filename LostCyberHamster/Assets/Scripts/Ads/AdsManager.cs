using UnityEngine;
using UnityEngine.Advertisements;
using Assets.Scripts;
using System;

namespace GameAds
{
    public static partial class AdsManager
    {
        private static string _gameId;
        private static string _androidAdUnitId = "Rewarded_Android";
        private static string _iOSAdUnitId = "Rewarded_iOS";
        private static string _adUnitId = null;
        private static bool _isInitialized = false;

        private static event Action OnAdLoaded;
        private static event Action<string> OnError;

        static AdsManager()
        {
#if UNITY_IOS
        _adUnitId = _iOSAdUnitId;
#elif UNITY_ANDROID
        _adUnitId = _androidAdUnitId;
#elif UNITY_EDITOR
            _adUnitId = _androidAdUnitId; //Only for testing the functionality in the Editor
#endif
        }

        public static void Initialize()
        {
            // Выбираем идентификатор игры для текущей платформы.
#if UNITY_IOS
            _gameId = Consts.IOS_GAME_ID;
#elif UNITY_ANDROID
            _gameId = Consts.ANDROID_GAME_ID;
#elif UNITY_EDITOR
            _gameId = Consts.ANDROID_GAME_ID; //Only for testing the functionality in the Editor
#endif

            // Используем готовый SDK либо запускаем его асинхронную инициализацию.
            if (Advertisement.isInitialized)
            {
                _isInitialized = true;
                return;
            }

            if (Advertisement.isSupported)
            {
                Advertisement.Initialize(_gameId, testMode: true, new AdsInitializationListener());
            }
        }

        public static void LoadAndShowAd()
        {
            if (!_isInitialized)
            {
                AdError("Unity Ads is not initialized yet!");
                return;
            }

            Advertisement.Load(_adUnitId, new AdLoadListener());
        }

        public static void ShowAd()
        {
            if (!_isInitialized)
            {
                AdError("Unity Ads is not initialized yet!");
                return;
            }

            Advertisement.Show(_adUnitId, new AdShowListener());
        }

        // Called by AdLoadListener when the ad is loaded
        private static void AdLoaded()
        {
            OnAdLoaded?.Invoke();
            ShowAd();
        }

        // Called by AdShowListener when the ad is completed
        private static void AdCompleted()
        {
            GameEventsManager.AdCompleted();
            GameEventsManager.AdFinished(true);
        }

        private static void AdCancelled()
        {
            GameEventsManager.AdFinished(false);
        }

        private static void AdError(string errorMessage)
        {
            OnError?.Invoke(errorMessage);
            GameEventsManager.AdFinished(false);
        }

        public static void OnEnable()
        {
            GameEventsManager.OnShowAd += LoadAndShowAd;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnShowAd -= LoadAndShowAd;
        }
    }
}
