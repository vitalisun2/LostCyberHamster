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
            GameObject page = ui.CreateScrollPage("RootNavigation", _rootObject.transform, out Transform content);
            ui.CreateSectionHeading("FeaturesHeading", content, "РАЗДЕЛЫ");
            ui.CreateBodyText(
                "FeaturesDescription",
                content,
                "Те же DEV-разделы, что и в Tools/Testing, но в fullscreen mobile layout с крупными зонами касания.");
            CreateNavigationCard(
                ui,
                content,
                "Account",
                "АККАУНТ",
                "Чистый старт, local reset и unlink тестового аккаунта.",
                () => AccountRequested?.Invoke());
            CreateNavigationCard(
                ui,
                content,
                "Gameplay",
                "GAMEPLAY И ПРОГРЕСС",
                "Живые toggles и оба progress testing-экрана в одной навигации.",
                () => GameplayRequested?.Invoke());
            CreateNavigationCard(
                ui,
                content,
                "Resources",
                "RESOURCES",
                "Точное DEV-начисление ресурсов без поиска по разным экранам.",
                () => ResourcesRequested?.Invoke());
            CreateNavigationCard(
                ui,
                content,
                "Networking",
                "NETWORKING",
                "Forced offline с тем же состоянием, что видно в editor Testing Tool.",
                () => NetworkingRequested?.Invoke());
            page.SetActive(true);
        }

        public event Action AccountRequested;
        public event Action GameplayRequested;
        public event Action ResourcesRequested;
        public event Action NetworkingRequested;

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

        private static void CreateNavigationCard(
            DevToolsUiFactory ui,
            Transform parent,
            string name,
            string title,
            string description,
            Action action)
        {
            Transform card = ui.CreateCard($"{name}Card", parent, DevToolsTheme.Surface);
            ui.CreateSectionHeading($"{name}Heading", card, title);
            ui.CreateBodyText($"{name}Description", card, description);
            ui.CreateButton(
                $"{name}Button",
                card,
                title,
                DevToolsTheme.Navigation,
                () => action?.Invoke(),
                DevToolsTheme.PrimaryButtonHeight);
        }
    }
}
#endif
