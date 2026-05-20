using UnityEngine;
using UnityEngine.Advertisements;

namespace GameAds
{
    public static partial class AdsManager
    {
        // Listener classes for handling ad events
        private class AdLoadListener : IUnityAdsLoadListener
        {
            public void OnUnityAdsAdLoaded(string adUnitId)
            {
                AdLoaded();
            }

            public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
            {
                string errorMessage = $"Error loading Ad Unit {adUnitId}: {error} - {message}";
                Debug.LogError(errorMessage);
                AdError(errorMessage);
            }
        }
    }
}
