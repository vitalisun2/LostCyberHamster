#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Композирует gameplay feature, управляет его lifecycle и связывает экран с общим DEV-shell.
    /// </summary>
    internal sealed class GameplayDevToolsScreen : IDevToolsScreen
    {
        private readonly GameplayDevToolsController _controller;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;

        public GameplayDevToolsScreen(
            Transform parent,
            Font font,
            Action closePanel,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;

            var uiFactory = new DevToolsUiFactory(font);
            var view = new GameplayDevToolsView(parent, uiFactory);
            _controller = new GameplayDevToolsController(new GameplayDevToolsService(), view, closePanel);
            RootObject = view.RootObject;
            _rootRect = RootObject.GetComponent<RectTransform>();
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Gameplay и прогресс");
            RefreshPresentation();
        }

        public void Hide()
        {
            RootObject.SetActive(false);
        }

        public void GoBack()
        {
            _returnToRoot?.Invoke();
        }

        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
        }

        public void RefreshPresentation()
        {
            _controller.RefreshPresentation();
        }
    }
}
#endif
