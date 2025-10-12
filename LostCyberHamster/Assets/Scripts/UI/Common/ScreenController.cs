using System;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public abstract class ScreenController: IScreenController
    {
        protected abstract ScreenEnum _screenAssetName { get; }

        protected VisualElement _background { get; private set; }

        public ScreenEnum Type => _screenAssetName;

        protected VisualElement _contentRoot;
        private AddressableLease<Sprite> _backgroundSpriteLease;

        protected ScreenController(UIDocument uiDocument)
        {
            _contentRoot = uiDocument.rootVisualElement.Q<VisualElement>("content") ?? uiDocument.rootVisualElement;
            _background = uiDocument.rootVisualElement.Q<VisualElement>("background");
        }

        public async Task LoadScreenAsync()
        {
            var asset = await Addressables.LoadAssetAsync<VisualTreeAsset>(_screenAssetName.ToString()).Task;
            _contentRoot.Clear();
            asset.CloneTree(_contentRoot);
            await OnLoadAsync();
            SubscribeToEvents();
        }

        public void SubscribeToEvents()
        {
            OnSubscribeToEvents();
        }

        public void UnsubscribeFromEvents()
        {
            OnUnsubscribeFromEvents();
            ReleaseBackgroundSprite();
        }

        protected async Task ChangeBackgroundAsync(string backgroundAssetName)
        {
            AddressableLease<Sprite> lease = null;
            var previousLease = _backgroundSpriteLease;
            try
            {
                lease = await AddressableLoader.LoadAssetAsync<Sprite>(backgroundAssetName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UI-BG] Failed to load sprite for key '{backgroundAssetName}'. Details: {ex.Message}");
                lease?.Dispose();
                return;
            }

            var sprite = lease?.Value;
            if (sprite == null)
            {
                Debug.LogError($"[UI-BG] Sprite FAILED to load for key '{backgroundAssetName}'");
                lease?.Dispose();
                return;
            }

            _backgroundSpriteLease = lease;
            _background.style.backgroundImage = new StyleBackground(sprite);
            previousLease?.Dispose();
        }

        protected void ReleaseBackgroundSprite()
        {
            _backgroundSpriteLease?.Dispose();
            _backgroundSpriteLease = null;
        }
        protected abstract Task OnLoadAsync();
        protected abstract void OnSubscribeToEvents();
        protected abstract void OnUnsubscribeFromEvents();
    }
}
