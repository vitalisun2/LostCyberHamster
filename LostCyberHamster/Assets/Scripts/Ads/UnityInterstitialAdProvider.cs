using System;
using UnityEngine.Advertisements;

namespace GameAds
{
    /// <summary>Отдельный placement interstitial, инициализация общая с rewarded.</summary>
    internal sealed class UnityInterstitialAdProvider : IUnityAdsLoadListener, IUnityAdsShowListener
    {
#if UNITY_IOS
        private const string Placement = "Interstitial_iOS";
#else
        private const string Placement = "Interstitial_Android";
#endif
        public bool IsReady { get; private set; }
        public bool IsLoading { get; private set; }
        private Action _started;
        private Action<bool> _closed;
        public void Load()
        {
            if (IsLoading || IsReady || !Advertisement.isInitialized) return;
            IsLoading = true;
            try { Advertisement.Load(Placement, this); }
            catch { IsLoading = false; throw; }
        }
        public void Show(Action started, Action<bool> closed)
        {
            if (!IsReady) { closed(false); return; }
            IsReady = false;
            _started = started;
            _closed = closed;
            Advertisement.Show(Placement, this);
        }
        public void OnUnityAdsAdLoaded(string id)
        {
            if (id != Placement) return;
            IsReady = true;
            IsLoading = false;
        }
        public void OnUnityAdsFailedToLoad(string id, UnityAdsLoadError error, string message)
        {
            if (id != Placement) return;
            IsLoading = false;
            IsReady = false;
        }
        public void OnUnityAdsShowStart(string id)
        {
            if (id == Placement) _started?.Invoke();
        }
        public void OnUnityAdsShowComplete(string id, UnityAdsShowCompletionState state) { if (id == Placement) Close(true); }
        public void OnUnityAdsShowFailure(string id, UnityAdsShowError error, string message) { if (id == Placement) Close(false); }
        public void OnUnityAdsShowClick(string id) { }
        private void Close(bool shown)
        {
            var callback = _closed;
            _closed = null;
            _started = null;
            callback?.Invoke(shown);
        }
    }
}
