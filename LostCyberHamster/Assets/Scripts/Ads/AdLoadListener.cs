using System;
using UnityEngine.Advertisements;

namespace GameAds
{
    public sealed partial class UnityRewardedAdProvider
    {
        private sealed class AdLoadListener : IUnityAdsLoadListener
        {
            private readonly string _placement;
            private readonly Action _loaded;
            private readonly Action<string> _failed;
            public AdLoadListener(string placement, Action loaded, Action<string> failed)
            {
                _placement = placement;
                _loaded = loaded;
                _failed = failed;
            }
            public void OnUnityAdsAdLoaded(string adUnitId)
            {
                if (adUnitId == _placement)
                    _loaded();
            }
            public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
            {
                if (adUnitId == _placement)
                    _failed($"{error}: {message}");
            }
        }
    }
}