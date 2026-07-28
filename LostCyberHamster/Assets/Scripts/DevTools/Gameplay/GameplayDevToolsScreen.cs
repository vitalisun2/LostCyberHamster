#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using Assets.Scripts.DevTools.ExperienceProgressTesting;
using Assets.Scripts.DevTools.GameProgressTesting;
using UnityEngine;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Композирует gameplay actions и оба progress testing экрана внутри DEV-shell.
    /// </summary>
    internal sealed class GameplayDevToolsScreen : IDevToolsScreen
    {
        private readonly GameplayDevToolsController _controller;
        private readonly GameplayDevToolsView _view;
        private readonly GameProgressTestingScreen _gameProgressTestingScreen;
        private readonly ExperienceProgressTestingScreen
            _experienceProgressTestingScreen;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;
        private bool _isGameProgressTestingOpen;
        private bool _isExperienceProgressTestingOpen;

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
            _experienceProgressTestingScreen =
                new ExperienceProgressTestingScreen(parent, font, setTitle);
            _controller = new GameplayDevToolsController(
                new GameplayDevToolsService(),
                _view,
                ShowGameProgressTesting,
                ShowExperienceProgressTesting);
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
            _experienceProgressTestingScreen.Hide();
        }

        public void GoBack()
        {
            if (_isGameProgressTestingOpen ||
                _isExperienceProgressTestingOpen)
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
            _experienceProgressTestingScreen.ApplyLayout(
                left,
                top,
                right,
                bottom);
        }

        public void RefreshPresentation()
        {
            if (_isGameProgressTestingOpen)
                _gameProgressTestingScreen.RefreshPresentation();
            else if (_isExperienceProgressTestingOpen)
                _experienceProgressTestingScreen.RefreshPresentation();
            else
                _controller.RefreshPresentation();
        }

        private void ShowGameplayActions()
        {
            _isGameProgressTestingOpen = false;
            _isExperienceProgressTestingOpen = false;
            _gameProgressTestingScreen.Hide();
            _experienceProgressTestingScreen.Hide();
            RootObject.SetActive(true);
            _setTitle?.Invoke("Gameplay и прогресс");
            _controller.RefreshPresentation();
        }

        private void ShowGameProgressTesting()
        {
            _isGameProgressTestingOpen = true;
            _isExperienceProgressTestingOpen = false;
            RootObject.SetActive(false);
            _experienceProgressTestingScreen.Hide();
            _gameProgressTestingScreen.Show();
        }

        private void ShowExperienceProgressTesting()
        {
            _isGameProgressTestingOpen = false;
            _isExperienceProgressTestingOpen = true;
            RootObject.SetActive(false);
            _gameProgressTestingScreen.Hide();
            _experienceProgressTestingScreen.Show();
        }
    }
}
#endif
