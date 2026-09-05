using System;
using UnityEngine.Advertisements;

namespace GameAds
{
    public sealed partial class UnityRewardedAdProvider
    {
        private sealed class AdShowListener : IUnityAdsShowListener
        {
            private readonly string _placement;
            private readonly Action _started;
            private readonly Action<bool> _completed;
            private readonly Action<string> _failed;
            public AdShowListener(string placement, Action started, Action<bool> completed, Action<string> failed)
            {
                _placement = placement;
                _started = started;
                _completed = completed;
                _failed = failed;
            }
            public void OnUnityAdsShowStart(string adUnitId)
            {
                if (adUnitId == _placement)
                    _started();
            }
            public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState state)
            {
                if (adUnitId == _placement)
                    _completed(state == UnityAdsShowCompletionState.COMPLETED);
            }
            public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
            {
                if (adUnitId == _placement)
                    _failed($"{error}: {message}");
            }
            public void OnUnityAdsShowClick(string adUnitId) { }
        }
    }
}