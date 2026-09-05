using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class UIManager
    {

        public static Action<ScreenEnum> OnScreenShow;
        public static Action<ScreenEnum> OnModalShow;

        public static Action OnRepaintScreen;
        public static Action OnRepaintModal;

        private ScreenEnum _currentScreen;
        private ScreenEnum? _currentModal;
        private bool _hasCurrentScreen;
        private bool _eventsSubscribed;
        private bool _lifecycleStarted;
        private bool _activeScreenEventsSubscribed;
        private bool _activeModalEventsSubscribed;
        private readonly SemaphoreSlim _transitionGate = new(1, 1);
        private CancellationTokenSource _screenTransitions = new();
        private readonly Dictionary<ScreenEnum, int>
            _modalTransitionVersions = new();

        private Dictionary<ScreenEnum, IScreenController> _screenControllers = new();

        public UIManager(IScreenController[] screenControllers)
        {
            foreach (var screenController in screenControllers)
            {
                AddScreenController(screenController);
            }
        }

        private async void OnScreenShowHandlerAsync(ScreenEnum screen)
        {
            await LoadScreenAsync(
                screen,
                forceReload: false,
                closeActiveModal: true);
        }

        private async void OnModalShowHandlerAsync(ScreenEnum modal)
        {
            await ShowModalAsync(modal);
        }


        public async Task LoadScreenAsync(ScreenEnum screen)
        {
            await LoadScreenAsync(
                screen,
                forceReload: false,
                closeActiveModal: false);
        }

        private async Task LoadScreenAsync(
            ScreenEnum screen,
            bool forceReload,
            bool closeActiveModal)
        {
            CancellationToken cancellationToken = _screenTransitions.Token;
            bool gateEntered = false;
            ScreenController previous = null;
            PreparedScreen prepared = null;
            try
            {
                // Запрос из завершённого lifecycle не должен оживлять старую panel.
                await _transitionGate.WaitAsync(cancellationToken);
                gateEntered = true;
                cancellationToken.ThrowIfCancellationRequested();
                if (!forceReload && _hasCurrentScreen &&
                    _activeScreenEventsSubscribed && _currentScreen == screen)
                    return;
                if (!_screenControllers.TryGetValue(screen, out var controller) ||
                    controller is not ScreenController next)
                    return;

                // Старое дерево и его ресурсы остаются видимыми во время подготовки.
                if (_hasCurrentScreen &&
                    _screenControllers.TryGetValue(_currentScreen, out var current))
                    previous = current as ScreenController;
                previous?.SetTransitionInputBlocked(true);
                prepared = await next.PrepareScreenAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Переключение выполняется целиком в одном синхронном участке.
                if (closeActiveModal && _currentModal.HasValue)
                    CloseModal(_currentModal.Value);
                if (_activeScreenEventsSubscribed)
                    previous?.UnsubscribeFromEvents();
                _activeScreenEventsSubscribed = false;
                PreparedScreen previousView = previous?.CurrentScreen;
                try
                {
                    next.ShowScreen(prepared);
                }
                catch
                {
                    // Восстанавливаем bindings прежнего дерева, включая Repaint того же controller.
                    next.UnsubscribeFromEvents();
                    prepared.Dispose();
                    if (previousView != null && !previousView.IsDisposed)
                    {
                        previous.ShowScreen(previousView);
                        _activeScreenEventsSubscribed = true;
                    }
                    throw;
                }
                prepared = null;
                _currentScreen = screen;
                _hasCurrentScreen = true;
                _activeScreenEventsSubscribed =
                    !_lifecycleStarted || _eventsSubscribed;
                if (!_activeScreenEventsSubscribed)
                    next.UnsubscribeFromEvents();
            }
            catch (OperationCanceledException)
            {
                // Отмена подготовки сохраняет прежний экран до закрытия его panel.
            }
            finally
            {
                prepared?.Dispose();
                previous?.SetTransitionInputBlocked(false);
                if (gateEntered)
                    _transitionGate.Release();
            }
        }

        public async void RepaintScreenAsync()
        {
            await LoadScreenAsync(
                _currentScreen,
                forceReload: true,
                closeActiveModal: false);
        }

        public void SubscribeToEvents()
        {
            if (_eventsSubscribed)
            {
                return;
            }

            if (_screenTransitions.IsCancellationRequested)
            {
                _screenTransitions.Dispose();
                _screenTransitions = new CancellationTokenSource();
            }

            OnScreenShow += OnScreenShowHandlerAsync;
            OnModalShow += OnModalShowHandlerAsync;
            OnRepaintScreen += RepaintScreenAsync;
            OnRepaintModal += RepaintModalAsync;
            _eventsSubscribed = true;
            _lifecycleStarted = true;

            // Восстанавливаем callbacks отображаемых элементов после повторного OnEnable.
            if (_hasCurrentScreen &&
                !_activeScreenEventsSubscribed &&
                _screenControllers.TryGetValue(
                    _currentScreen,
                    out var currentScreenController))
            {
                currentScreenController.SubscribeToEvents();
                _activeScreenEventsSubscribed = true;
            }

            if (_currentModal.HasValue &&
                !_activeModalEventsSubscribed &&
                _screenControllers.TryGetValue(
                    _currentModal.Value,
                    out var currentModalController))
            {
                currentModalController.SubscribeToEvents();
                _activeModalEventsSubscribed = true;
            }
        }

        public void UnsubscribeFromEvents()
        {
            _screenTransitions.Cancel();
            if (!_eventsSubscribed)
            {
                return;
            }

            // Снимаем глобальные маршруты UI.
            OnScreenShow -= OnScreenShowHandlerAsync;
            OnModalShow -= OnModalShowHandlerAsync;
            OnRepaintScreen -= RepaintScreenAsync;
            OnRepaintModal -= RepaintModalAsync;
            _eventsSubscribed = false;

            // Освобождаем callbacks и ресурсы активных экранов.
            if (_hasCurrentScreen &&
                _activeScreenEventsSubscribed &&
                _screenControllers.TryGetValue(
                    _currentScreen,
                    out var currentScreenController))
            {
                currentScreenController.UnsubscribeFromEvents();
                _activeScreenEventsSubscribed = false;
            }

            if (_currentModal.HasValue &&
                _activeModalEventsSubscribed &&
                _screenControllers.TryGetValue(
                    _currentModal.Value,
                    out var currentModalController))
            {
                currentModalController.UnsubscribeFromEvents();
                _activeModalEventsSubscribed = false;
            }
        }

        public async Task ShowModalAsync(ScreenEnum modal)
        {
            int transitionVersion = BeginModalTransition(modal);
            await _transitionGate.WaitAsync();
            try
            {
                if (!IsCurrentModalTransition(
                        modal,
                        transitionVersion))
                {
                    return;
                }

                if (!_screenControllers.TryGetValue(
                        modal,
                        out var modalController))
                {
                    return;
                }

                if (_currentModal.HasValue &&
                    _activeModalEventsSubscribed &&
                    _screenControllers.TryGetValue(
                        _currentModal.Value,
                        out var currentModalController))
                {
                    currentModalController.UnsubscribeFromEvents();
                    _activeModalEventsSubscribed = false;
                }

                _currentModal = null;
                await (modalController as ModalController).ShowAsync();
                if (!IsCurrentModalTransition(
                        modal,
                        transitionVersion))
                {
                    modalController.UnsubscribeFromEvents();
                    (modalController as ModalController).Close();
                    return;
                }

                _currentModal = modal;
                _activeModalEventsSubscribed =
                    !_lifecycleStarted || _eventsSubscribed;
                if (!_activeModalEventsSubscribed)
                {
                    modalController.UnsubscribeFromEvents();
                }
            }
            finally
            {
                _transitionGate.Release();
            }
        }

        public void HideModal(ScreenEnum modal)
        {
            BeginModalTransition(modal);
            if (_screenControllers.TryGetValue(modal, out var modalController))
            {
                if (_currentModal == modal &&
                    _activeModalEventsSubscribed)
                {
                    modalController.UnsubscribeFromEvents();
                    _activeModalEventsSubscribed = false;
                }

                if (_currentModal == modal)
                {
                    (modalController as ModalController).Hide();
                    _currentModal = null;
                }
            }
        }

        /// <summary>Закрывает модальное окно.</summary>
        public void CloseModal(ScreenEnum modal)
        {
            BeginModalTransition(modal);
            if (_screenControllers.TryGetValue(modal, out var modalController))
            {
                if (_currentModal == modal &&
                    _activeModalEventsSubscribed)
                {
                    modalController.UnsubscribeFromEvents();
                    _activeModalEventsSubscribed = false;
                }

                if (_currentModal == modal)
                {
                    (modalController as ModalController).Close();
                    _currentModal = null;
                }
            }
        }

        private int BeginModalTransition(ScreenEnum modal)
        {
            _modalTransitionVersions.TryGetValue(
                modal,
                out int currentVersion);
            int nextVersion = currentVersion + 1;
            _modalTransitionVersions[modal] = nextVersion;
            return nextVersion;
        }

        private bool IsCurrentModalTransition(
            ScreenEnum modal,
            int transitionVersion)
        {
            return _modalTransitionVersions.TryGetValue(
                       modal,
                       out int currentVersion) &&
                   currentVersion == transitionVersion;
        }

        private void AddScreenController(IScreenController screenController)
        {
            _screenControllers.Add(screenController.Type, screenController);
        }

        internal async void RepaintModalAsync()
        {
            if (_currentModal.HasValue)
            {
                await ShowModalAsync(_currentModal.Value);
            }
        }

        internal T GetController<T>() where T : IScreenController
        {
            foreach (var screenController in _screenControllers.Values)
            {
                if (screenController is T controller)
                {
                    return controller;
                }
            }

            throw new Exception("Controller not found");
        }

    }

}
