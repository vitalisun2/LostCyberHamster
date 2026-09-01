using UnityEngine;
using UnityEngine.Advertisements;

namespace GameAds
{
    public static partial class AdsManager
    {
        private class AdShowListener : IUnityAdsShowListener
        {
            public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
            {
                string errorMessage = $"Error showing Ad Unit {adUnitId}: {error} - {message}";
                Debug.LogError(errorMessage);
                AdError(errorMessage);
            }

            public void OnUnityAdsShowStart(string adUnitId)
            {

            }

            public void OnUnityAdsShowClick(string adUnitId)
            {

            }

            public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
            {
                if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
                {
                    AdCompleted();
                    return;
                }

                AdCancelled();
            }
        }
    }
}
