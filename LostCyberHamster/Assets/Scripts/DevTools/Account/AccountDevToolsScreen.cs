#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Представляет пустой account-раздел DEV-меню для последующей пошаговой реализации фичи.
    /// </summary>
    internal sealed class AccountDevToolsScreen : IDevToolsScreen
    {
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;

        public AccountDevToolsScreen(
            Transform parent,
            Font font,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;

            var uiFactory = new DevToolsUiFactory(font);
            RootObject = uiFactory.CreateUiObject("AccountScreen", parent);
            _rootRect = RootObject.GetComponent<RectTransform>();
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Аккаунт");
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
        }
    }
}
#endif
