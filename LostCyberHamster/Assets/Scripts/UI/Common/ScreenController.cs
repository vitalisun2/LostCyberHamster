using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Разделяет подготовку геометрии экрана, его показ и загрузку данных.</summary>
    public abstract class ScreenController : IScreenController
    {
        protected abstract ScreenEnum _screenAssetName { get; }
        protected virtual string ScreenBackgroundAddress => null;
        protected VisualElement _background { get; private set; }
        protected VisualElement _contentRoot;

        private readonly VisualElement _container;
        private readonly VisualElement _backgroundHost;
        private PreparedScreen _screen;
        private bool _eventsSubscribed;
        private bool _initializing;

        public ScreenEnum Type => _screenAssetName;
        internal PreparedScreen CurrentScreen => _screen;

        protected ScreenController(UIDocument uiDocument)
        {
            _container = uiDocument.rootVisualElement.Q<VisualElement>("content")
                         ?? uiDocument.rootVisualElement;
            _backgroundHost = uiDocument.rootVisualElement.Q<VisualElement>("background");
            _contentRoot = _container;
        }

        /// <summary>Готовит отдельное дерево, сохраняя состояние ещё видимого контроллера.</summary>
        internal async Task<PreparedScreen> PrepareScreenAsync(CancellationToken cancellationToken)
        {
            AddressableLease<VisualTreeAsset> lease =
                await AddressableLoader.LoadAssetAsync<VisualTreeAsset>(
                    _screenAssetName.ToString(), cancellationToken);
            PreparedScreen prepared = null;
            try
            {
                if (lease.Value == null)
                    throw new InvalidOperationException($"Экран '{Type}' не содержит VisualTreeAsset.");

                // Новый слой владеет своими ассетами и запросами элементов.
                prepared = new PreparedScreen(lease);
                lease.Value.CloneTree(prepared.Content);
                if (prepared.Content.childCount == 0)
                    throw new InvalidOperationException($"Экран '{Type}' не создал visual tree.");
                prepared.Content.Query<SharedSettingsButton>().ForEach(
                    button => button.SetOriginScreen(_screenAssetName));
                prepared.Layout = CreateLayout(prepared.Content);

                // Адаптивные меню рассчитываются скрытыми в окончательном контейнере.
                if (prepared.Layout != null)
                    _container.Insert(0, prepared.Root);
                using (cancellationToken.Register(prepared.Dispose))
                {
                    if (!string.IsNullOrEmpty(ScreenBackgroundAddress))
                    {
                        var background = await AddressableLoader.LoadAssetAsync<Sprite>(
                            ScreenBackgroundAddress, cancellationToken);
                        if (prepared.IsDisposed)
                            background.Dispose();
                        else
                            prepared.SetBackground(background);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (prepared.Layout != null)
                        await prepared.Layout.Ready;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (prepared.IsDisposed || _container.panel == null)
                    throw new OperationCanceledException("Panel экрана уже закрыта.");
                return prepared;
            }
            catch
            {
                if (prepared != null)
                    prepared.Dispose();
                else
                    lease.Dispose();
                throw;
            }
        }

        /// <summary>Активирует bindings и показывает готовый слой; данные догружаются отдельно.</summary>
        internal void ShowScreen(PreparedScreen prepared)
        {
            _screen = prepared;
            _background = _backgroundHost;
            _contentRoot = prepared.Content;
            prepared.Detached += () =>
            {
                if (_screen == prepared)
                    UnsubscribeFromEvents();
            };

            // Bindings и места для данных существуют до первого видимого кадра.
            _initializing = true;
            BindView();
            SubscribeToEvents();
            _initializing = false;
            prepared.ApplyBackground(_backgroundHost);
            prepared.Present(_container);
            ObserveDataLoadAsync(prepared);
        }

        private async void ObserveDataLoadAsync(PreparedScreen screen)
        {
            try
            {
                await LoadDataAsync();
            }
            catch (OperationCanceledException)
            {
                // Уход с экрана завершает принадлежащие ему загрузки.
            }
            catch (Exception exception)
            {
                if (_screen == screen && !screen.IsDisposed)
                    Debug.LogError($"[UI] Не удалось загрузить данные '{Type}': {exception}");
            }
        }

        internal void SetTransitionInputBlocked(bool blocked)
        {
            _screen?.SetInputBlocked(blocked);
        }

        public void SubscribeToEvents()
        {
            if (_eventsSubscribed)
                return;
            _eventsSubscribed = true;
            OnSubscribeToEvents();
        }

        public void UnsubscribeFromEvents()
        {
            if (!_eventsSubscribed && !_initializing)
                return;
            _eventsSubscribed = false;
            _initializing = false;
            OnUnsubscribeFromEvents();
        }

        protected virtual ScreenLayout CreateLayout(VisualElement content)
        {
            return null;
        }

        protected virtual Task LoadDataAsync()
        {
            return Task.CompletedTask;
        }

        protected abstract void BindView();
        protected abstract void OnSubscribeToEvents();
        protected abstract void OnUnsubscribeFromEvents();
    }
}
