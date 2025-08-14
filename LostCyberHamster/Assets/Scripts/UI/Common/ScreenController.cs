using System;
using System.Threading.Tasks;
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
        }

        protected async Task ChangeBackgroundAsync(string backgroundAssetName)
        {
            var asset = await Addressables.LoadAssetAsync<Texture2D>(backgroundAssetName).Task;
            _background.style.backgroundImage = asset;
        }
        protected abstract Task OnLoadAsync();
        protected abstract void OnSubscribeToEvents();
        protected abstract void OnUnsubscribeFromEvents();
    }
}
