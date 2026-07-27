#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using Assets.Scripts.DevTools.GameProgressTesting;
using UnityEngine;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Композирует gameplay actions и вложенный Game Progress Testing, управляя их навигацией в DEV-shell.
    /// </summary>
    internal sealed class GameplayDevToolsScreen : IDevToolsScreen
    {
        private readonly GameplayDevToolsController _controller;
        private readonly GameplayDevToolsView _view;
        private readonly GameProgressTestingScreen _gameProgressTestingScreen;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;
        private bool _isGameProgressTestingOpen;

        public GameplayDevToolsScreen(
            Transform parent,
            Font font,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;

            var uiFactory = new DevToolsUiFactory(font);
            _view = new GameplayDevToolsView(parent, uiFactory);
            _gameProgressTestingScreen = new GameProgressTestingScreen(parent, font, setTitle);
            _controller = new GameplayDevToolsController(
                new GameplayDevToolsService(),
                _view,
                ShowGameProgressTesting);
            RootObject = _view.RootObject;
            _rootRect = RootObject.GetComponent<RectTransform>();
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            ShowGameplayActions();
        }

        public void Hide()
        {
            RootObject.SetActive(false);
            _gameProgressTestingScreen.Hide();
        }

        public void GoBack()
        {
            if (_isGameProgressTestingOpen)
            {
                ShowGameplayActions();
                return;
            }

            _returnToRoot?.Invoke();
        }

        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
            _gameProgressTestingScreen.ApplyLayout(left, top, right, bottom);
        }

        public void RefreshPresentation()
        {
            if (_isGameProgressTestingOpen)
                _gameProgressTestingScreen.RefreshPresentation();
            else
                _controller.RefreshPresentation();
        }

        private void ShowGameplayActions()
        {
            _isGameProgressTestingOpen = false;
            _gameProgressTestingScreen.Hide();
            RootObject.SetActive(true);
            _setTitle?.Invoke("Gameplay и прогресс");
            _controller.RefreshPresentation();
        }

        private void ShowGameProgressTesting()
        {
            _isGameProgressTestingOpen = true;
            RootObject.SetActive(false);
            _gameProgressTestingScreen.Show();
        }
    }
}
#endif
