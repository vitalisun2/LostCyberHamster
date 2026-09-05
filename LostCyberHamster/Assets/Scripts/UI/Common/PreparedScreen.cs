using System;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Владеет деревом, фоном и геометрией одного подготовленного экрана.</summary>
    internal sealed class PreparedScreen : IDisposable
    {
        private readonly AddressableLease<VisualTreeAsset> _asset;
        private AddressableLease<Sprite> _background;
        private bool _disposed;
        private bool _inputBlocked;

        public VisualElement Root { get; }
        public VisualElement Content { get; }
        public ScreenLayout Layout { get; set; }
        public bool IsDisposed => _disposed;
        public event Action Detached;

        public PreparedScreen(AddressableLease<VisualTreeAsset> asset)
        {
            _asset = asset;
            Root = new VisualElement { name = "screen-layer" };
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.right = 0;
            Root.style.top = 0;
            Root.style.bottom = 0;
            Root.style.visibility = Visibility.Hidden;
            Root.style.opacity = 0f;
            Content = new VisualElement { name = "screen-content" };
            Content.style.flexGrow = 1;
            Root.Add(Content);
            Root.RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        public void SetBackground(AddressableLease<Sprite> background)
        {
            _background = background;
            if (background.Value == null)
                throw new InvalidOperationException("Фон экрана не содержит Sprite.");
        }

        /// <summary>Применяет подготовленный фон к полноэкранному host вне safe area.</summary>
        public void ApplyBackground(VisualElement host)
        {
            if (host == null)
                return;
            host.style.backgroundImage = _background == null
                ? new StyleBackground(StyleKeyword.None)
                : new StyleBackground(_background.Value);
            host.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            host.style.backgroundPositionX =
                new BackgroundPosition(BackgroundPositionKeyword.Center);
            host.style.backgroundPositionY =
                new BackgroundPosition(BackgroundPositionKeyword.Center);
            host.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
        }

        /// <summary>Переключает готовые фон и содержимое без промежуточного пустого кадра.</summary>
        public void Present(VisualElement container)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PreparedScreen));

            // Game/Intro подключаются только при активации готовых bindings.
            if (Root.parent == null)
                container.Add(Root);
            Root.style.visibility = Visibility.Visible;
            Root.style.opacity = 1f;

            // Старый слой освобождает собственные ресурсы при detach.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                VisualElement child = container[i];
                if (child != Root)
                    child.RemoveFromHierarchy();
            }
        }

        public void SetInputBlocked(bool blocked)
        {
            if (_inputBlocked == blocked || _disposed)
                return;
            _inputBlocked = blocked;
            if (blocked)
            {
                Root.RegisterCallback<PointerDownEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.RegisterCallback<PointerUpEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.RegisterCallback<ClickEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.RegisterCallback<NavigationSubmitEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.RegisterCallback<KeyDownEvent>(BlockInput, TrickleDown.TrickleDown);
            }
            else
            {
                Root.UnregisterCallback<PointerDownEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.UnregisterCallback<PointerUpEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.UnregisterCallback<ClickEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.UnregisterCallback<NavigationSubmitEvent>(BlockInput, TrickleDown.TrickleDown);
                Root.UnregisterCallback<KeyDownEvent>(BlockInput, TrickleDown.TrickleDown);
            }
        }

        private static void BlockInput(EventBase evt)
        {
            evt.StopImmediatePropagation();
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (evt.target != Root)
                return;
            try
            {
                Detached?.Invoke();
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Root.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            Root.RemoveFromHierarchy();
            Layout?.Dispose();
            _background?.Dispose();
            _asset.Dispose();
            Detached = null;
        }
    }
}
