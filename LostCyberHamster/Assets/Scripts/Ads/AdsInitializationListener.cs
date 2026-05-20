using UnityEngine;
using UnityEngine.Advertisements;

namespace GameAds
{
    public static partial class AdsManager
    {
        private class AdsInitializationListener : IUnityAdsInitializationListener
        {
            public void OnInitializationComplete()
            {
                _isInitialized = true;
            }

            public void OnInitializationFailed(UnityAdsInitializationError error, string message)
            {
                Debug.LogError($"Unity Ads Initialization Failed: {error} - {message}");
                _isInitialized = false;
            }
        }
    }
}
