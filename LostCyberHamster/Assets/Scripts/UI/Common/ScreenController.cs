using System;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Управляет загрузкой и жизненным циклом одного UI Toolkit экрана.
    /// </summary>
    public abstract class ScreenController: IScreenController
    {
        protected abstract ScreenEnum _screenAssetName { get; }

        protected VisualElement _background { get; private set; }

        public ScreenEnum Type => _screenAssetName;

        protected VisualElement _contentRoot;
        private AddressableLease<Sprite> _backgroundSpriteLease;
        private AddressableLease<VisualTreeAsset> _screenAssetLease;
        private VisualElement _screenRoot;

        protected ScreenController(UIDocument uiDocument)
        {
            _contentRoot = uiDocument.rootVisualElement.Q<VisualElement>("content") ?? uiDocument.rootVisualElement;
            _background = uiDocument.rootVisualElement.Q<VisualElement>("background");
        }

        /// <summary>
        /// Загружает visual tree экрана и удерживает его Addressables lease до удаления дерева из panel.
        /// </summary>
        public async Task LoadScreenAsync()
        {
            // Загружаем новый asset до удаления текущего дерева.
            AddressableLease<VisualTreeAsset> lease =
                await AddressableLoader.LoadAssetAsync<VisualTreeAsset>(
                    _screenAssetName.ToString());
            VisualTreeAsset asset = lease.Value;
            if (asset == null)
            {
                lease.Dispose();
                throw new InvalidOperationException(
                    $"Экран '{_screenAssetName}' не содержит VisualTreeAsset.");
            }

            try
            {
                // Замена прежнего дерева освобождает его lease через DetachFromPanelEvent.
                _contentRoot.Clear();
                asset.CloneTree(_contentRoot);
                if (_contentRoot.childCount == 0)
                {
                    throw new InvalidOperationException(
                        $"Экран '{_screenAssetName}' не создал visual tree.");
                }

                // Новое дерево удерживает asset до своего удаления из panel.
                _screenRoot = _contentRoot[0];
                _screenRoot.RegisterCallback<DetachFromPanelEvent>(
                    OnScreenRootDetached);
                _screenAssetLease = lease;
                lease = null;
            }
            catch
            {
                _contentRoot.Clear();
                _screenRoot = null;
                lease?.Dispose();
                throw;
            }

            _contentRoot.Query<SharedSettingsButton>().ForEach(
                button => button.SetOriginScreen(_screenAssetName));
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

        /// <summary>
        /// Загружает фон экрана и применяет заданный режим масштабирования.
        /// </summary>
        protected async Task ChangeBackgroundAsync(
            string backgroundAssetName,
            ScaleMode scaleMode = ScaleMode.StretchToFill)
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
            _background.style.backgroundSize = scaleMode switch
            {
                ScaleMode.ScaleAndCrop =>
                    new BackgroundSize(BackgroundSizeType.Cover),
                ScaleMode.ScaleToFit =>
                    new BackgroundSize(BackgroundSizeType.Contain),
                _ => new BackgroundSize(
                    Length.Percent(100),
                    Length.Percent(100))
            };
            _background.style.backgroundPositionX =
                new BackgroundPosition(BackgroundPositionKeyword.Center);
            _background.style.backgroundPositionY =
                new BackgroundPosition(BackgroundPositionKeyword.Center);
            _background.style.backgroundRepeat = new BackgroundRepeat(
                Repeat.NoRepeat,
                Repeat.NoRepeat);
            previousLease?.Dispose();
        }

        protected void ReleaseBackgroundSprite()
        {
            _backgroundSpriteLease?.Dispose();
            _backgroundSpriteLease = null;
        }

        private void OnScreenRootDetached(DetachFromPanelEvent detachEvent)
        {
            // Игнорируем detach дочерних элементов.
            if (detachEvent.target != _screenRoot)
            {
                return;
            }

            // Освобождаем asset после удаления корня экрана из panel.
            _screenRoot.UnregisterCallback<DetachFromPanelEvent>(
                OnScreenRootDetached);
            _screenRoot = null;
            _screenAssetLease?.Dispose();
            _screenAssetLease = null;
        }

        protected abstract Task OnLoadAsync();
        protected abstract void OnSubscribeToEvents();
        protected abstract void OnUnsubscribeFromEvents();
    }
}
