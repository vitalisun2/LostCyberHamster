using System;
using System.Collections.Generic;
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
            _currentScreen = screen;
            await LoadScreenAsync(screen);
        }

        private async void OnModalShowHandlerAsync(ScreenEnum modal)
        {
            await ShowModalAsync(modal);
        }


        public async Task LoadScreenAsync(ScreenEnum screen)
        {
            if (_screenControllers.TryGetValue(screen, out var screenController))
            {
                _currentScreen = screen;
                await (screenController as ScreenController).LoadScreenAsync();
            }
        }

        public async void RepaintScreenAsync()
        {
            await LoadScreenAsync(_currentScreen);
        }

        public void SubscribeToEvents()
        {
            OnScreenShow += OnScreenShowHandlerAsync;
            OnModalShow += OnModalShowHandlerAsync;
            OnRepaintScreen += RepaintScreenAsync;
            OnRepaintModal += RepaintModalAsync;
            foreach (var screenController in _screenControllers.Values)
            {
                screenController.SubscribeToEvents();
            }
        }

        public void UnsubscribeFromEvents()
        {
            OnScreenShow -= OnScreenShowHandlerAsync;
            OnModalShow -= OnModalShowHandlerAsync;
            OnRepaintScreen -= RepaintScreenAsync;
            foreach (var screenController in _screenControllers.Values)
            {
                screenController.UnsubscribeFromEvents();
            }
        }

        public async Task ShowModalAsync(ScreenEnum modal)
        {
            if (_screenControllers.TryGetValue(modal, out var modalController))
            {
                if (_currentModal.HasValue &&
                    _screenControllers.TryGetValue(_currentModal.Value, out var currentModalController))
                {
                    currentModalController.UnsubscribeFromEvents();
                }

                _currentModal = modal;
                await (modalController as ModalController).ShowAsync();
            }
        }

        public void HideModal(ScreenEnum modal)
        {
            if (_screenControllers.TryGetValue(modal, out var modalController))
            {
                modalController.UnsubscribeFromEvents();
                (modalController as ModalController).Hide();
                _currentModal = null;
            }
        }

        /// <summary>Закрывает модальное окно.</summary>
        public void CloseModal(ScreenEnum modal)
        {
            if (_screenControllers.TryGetValue(modal, out var modalController))
            {
                modalController.UnsubscribeFromEvents();
                (modalController as ModalController).Close();
                _currentModal = null;
            }
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
