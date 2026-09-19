using System;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Удерживает UI-bundle активностей, пока UI Toolkit хранит вычисленные стили.</summary>
    internal sealed class ReturnActivitiesAssetLifetime : MonoBehaviour
    {
        private static ReturnActivitiesAssetLifetime _instance;
        private AddressableLease<VisualTreeAsset> _asset;
        private Task _ready;

        internal static Task Ready
        {
            get
            {
                EnsureInstance();
                return _instance._ready;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureInstance();

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var host = new GameObject(nameof(ReturnActivitiesAssetLifetime));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ReturnActivitiesAssetLifetime>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _ready = RetainAsync();
        }

        private async Task RetainAsync()
        {
            _asset = await AddressableLoader.LoadAssetAsync<VisualTreeAsset>(
                ScreenEnum.ReturnActivitiesScreen.ToString());
            if (_asset.Value == null)
                throw new InvalidOperationException("ReturnActivitiesScreen не содержит VisualTreeAsset.");
        }

        private void OnDestroy()
        {
            _asset?.Dispose();
            _asset = null;
            if (_instance == this)
                _instance = null;
        }
    }
}
