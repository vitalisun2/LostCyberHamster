using System;
using System.Threading.Tasks;
using UnityEngine.Advertisements;

namespace GameAds
{
    public sealed partial class UnityRewardedAdProvider
    {
        private sealed class AdsInitializationListener : IUnityAdsInitializationListener
        {
            private readonly TaskCompletionSource<bool> _completion;
            public AdsInitializationListener(TaskCompletionSource<bool> completion) => _completion = completion;
            public void OnInitializationComplete() => _completion.TrySetResult(true);
            public void OnInitializationFailed(UnityAdsInitializationError error, string message) =>
                _completion.TrySetException(new InvalidOperationException($"{error}: {message}"));
        }
    }
}