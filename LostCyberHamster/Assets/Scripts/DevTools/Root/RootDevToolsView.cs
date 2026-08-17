#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools.Root
{
    /// <summary>
    /// Отображает корневую навигацию DEV-меню без feature-specific действий.
    /// </summary>
    internal sealed class RootDevToolsView
    {
        private readonly GameObject _rootObject;
        private readonly RectTransform _rootRect;

        public RootDevToolsView(Transform parent, Font font)
        {
            DevToolsUiFactory ui = new DevToolsUiFactory(font);
            _rootObject = ui.CreateUiObject("RootScreen", parent);
            _rootRect = _rootObject.GetComponent<RectTransform>();
            GameObject page = ui.CreateStaticPage("RootNavigation", _rootObject.transform, out Transform content);
            ui.CreateSectionHeading("FeaturesHeading", content, "РАЗДЕЛЫ");
            ui.CreateButton(
                "AccountButton",
                content,
                "АККАУНТ",
                DevToolsTheme.Navigation,
                () => AccountRequested?.Invoke(),
                DevToolsTheme.PrimaryButtonHeight);
            ui.CreateButton(
                "GameplayButton",
                content,
                "GAMEPLAY И ПРОГРЕСС",
                DevToolsTheme.Navigation,
                () => GameplayRequested?.Invoke(),
                DevToolsTheme.PrimaryButtonHeight);
            ui.CreateButton(
                "ResourcesButton",
                content,
                "Resources",
                DevToolsTheme.Navigation,
                () => ResourcesRequested?.Invoke(),
                DevToolsTheme.PrimaryButtonHeight);
            page.SetActive(true);
        }

        public event Action AccountRequested;
        public event Action GameplayRequested;
        public event Action ResourcesRequested;

        public GameObject RootObject => _rootObject;

        public void SetVisible(bool visible)
        {
            _rootObject.SetActive(visible);
        }

        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
        }
    }
}
#endif
