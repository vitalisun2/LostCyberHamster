#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using Assets.Scripts.Online;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Networking
{
    /// <summary>Управляет сохраняемым режимом офлайна через общий сетевой фасад.</summary>
    internal sealed class NetworkingDevToolsScreen : IDevToolsScreen
    {
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly GameNetworkFacade _network;
        private readonly RectTransform _rootRect;
        private readonly Button _toggleButton;
        private readonly Text _buttonText;
        private readonly Text _statusText;
        private string _error;

        public NetworkingDevToolsScreen(Transform parent, Font font,
            Action returnToRoot, Action<string> setTitle)
        {
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;
            _network = GameNetworkFacade.Instance;
            var ui = new DevToolsUiFactory(font);
            RootObject = ui.CreateStaticPage("NetworkingScreen", parent, out Transform content);
            _rootRect = RootObject.GetComponent<RectTransform>();
            ui.CreateSectionHeading("NetworkingHeading", content, "Networking");
            _toggleButton = ui.CreateButton("NetworkToggleButton", content, "Turn off network",
                DevToolsTheme.Primary, ToggleNetwork, DevToolsTheme.PrimaryButtonHeight);
            _buttonText = _toggleButton.GetComponentInChildren<Text>();
            Transform statusCard = ui.CreateCard("NetworkStatusCard", content, DevToolsTheme.StatusCard);
            _statusText = ui.CreateBodyText("NetworkStatus", statusCard, string.Empty);
            ui.CreateBodyText("NetworkPersistence", content, "Режим сохраняется после перезапуска");
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Networking");
            RefreshPresentation();
        }

        public void Hide() => RootObject.SetActive(false);
        public void GoBack() => _returnToRoot?.Invoke();

        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
        }

        public void RefreshPresentation()
        {
            bool offline = _network.IsForcedOffline;
            _buttonText.text = offline ? "Turn on network" : "Turn off network";
            _toggleButton.GetComponent<Image>().color = offline ? DevToolsTheme.Enabled : DevToolsTheme.Danger;
            _statusText.text = _error ?? (offline
                ? "Симуляция офлайна включена"
                : "Сетевые обращения разрешены");
        }

        private void ToggleNetwork()
        {
            try
            {
                _network.SetForcedOffline(!_network.IsForcedOffline);
                _error = null;
            }
            catch (Exception exception)
            {
                _error = $"Не удалось сохранить режим: {exception.Message}";
            }
            RefreshPresentation();
        }
    }
}
#endif
