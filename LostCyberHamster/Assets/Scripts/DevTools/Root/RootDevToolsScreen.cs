#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools.Root
{
    /// <summary>
    /// Координирует lifecycle корневого списка feature-разделов DEV-меню.
    /// </summary>
    internal sealed class RootDevToolsScreen : IDevToolsScreen
    {
        private readonly RootDevToolsView _view;
        private readonly Action _closePanel;
        private readonly Action<string> _setTitle;

        public RootDevToolsScreen(
            Transform parent,
            Font font,
            Action closePanel,
            Action showAccount,
            Action showGameplay,
            Action showResources,
            Action<string> setTitle)
        {
            _closePanel = closePanel;
            _setTitle = setTitle;
            _view = new RootDevToolsView(parent, font);
            _view.AccountRequested += showAccount;
            _view.GameplayRequested += showGameplay;
            _view.ResourcesRequested += showResources;
        }

        public GameObject RootObject => _view.RootObject;

        public void Show()
        {
            _view.SetVisible(true);
            _setTitle("Developer");
        }

        public void Hide()
        {
            _view.SetVisible(false);
        }

        public void GoBack()
        {
            _closePanel();
        }

        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _view.ApplyLayout(left, top, right, bottom);
        }

        public void RefreshPresentation()
        {
        }
    }
}
#endif
