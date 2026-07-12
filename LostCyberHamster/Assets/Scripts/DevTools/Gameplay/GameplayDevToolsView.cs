#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Создаёт gameplay-представление общими UI-примитивами и отображает подготовленное состояние контроллера.
    /// </summary>
    internal sealed class GameplayDevToolsView
    {
        private readonly Button _botButton;
        private readonly Text _botButtonText;
        private readonly Image _botButtonImage;
        private readonly Button _unlockAllButton;
        private readonly Text _unlockAllButtonText;
        private readonly Image _unlockAllButtonImage;
        private readonly Button _completeLevelButton;
        private readonly Button _resetProgressButton;
        private readonly GameObject _statusCard;
        private readonly Text _statusText;

        public GameplayDevToolsView(Transform parent, DevToolsUiFactory uiFactory)
        {
            RootObject = uiFactory.CreateStaticPage("GameplayScreen", parent, out Transform content);

            uiFactory.CreateSectionHeading("ActionsHeading", content, "GAMEPLAY И ПРОГРЕСС");
            _botButton = uiFactory.CreateButton(
                "BotButton",
                content,
                "Bot Off",
                DevToolsTheme.Disabled,
                () => BotToggleRequested?.Invoke());
            _botButtonText = _botButton.GetComponentInChildren<Text>();
            _botButtonImage = _botButton.GetComponent<Image>();

            _unlockAllButton = uiFactory.CreateButton(
                "UnlockAllButton",
                content,
                "Unlock All Off",
                DevToolsTheme.Disabled,
                () => UnlockAllToggleRequested?.Invoke());
            _unlockAllButtonText = _unlockAllButton.GetComponentInChildren<Text>();
            _unlockAllButtonImage = _unlockAllButton.GetComponent<Image>();

            _completeLevelButton = uiFactory.CreateButton(
                "CompleteLevelButton",
                content,
                "Complete Level (3 Stars)",
                DevToolsTheme.Button,
                () => CompleteLevelRequested?.Invoke());
            _resetProgressButton = uiFactory.CreateButton(
                "ResetProgressButton",
                content,
                "Reset Progress",
                DevToolsTheme.Button,
                () => ResetProgressRequested?.Invoke());

            Transform statusCard = uiFactory.CreateCard("StatusCard", content, DevToolsTheme.StatusCard);
            _statusCard = statusCard.gameObject;
            _statusText = uiFactory.CreateBodyText("StatusText", statusCard, string.Empty);
        }

        public event Action BotToggleRequested;
        public event Action UnlockAllToggleRequested;
        public event Action CompleteLevelRequested;
        public event Action ResetProgressRequested;

        public GameObject RootObject { get; }

        public void Render(GameplayDevToolsPresentation presentation)
        {
            _botButtonText.text = presentation.BotLabel;
            _botButtonImage.color = presentation.BotEnabled ? DevToolsTheme.Enabled : DevToolsTheme.Disabled;
            _botButton.interactable = presentation.BotActionAvailable;

            _unlockAllButtonText.text = presentation.UnlockAllLabel;
            _unlockAllButtonImage.color = presentation.UnlockAllLevels ? DevToolsTheme.Enabled : DevToolsTheme.Disabled;
            _unlockAllButton.interactable = presentation.ActionsAvailable;
            _completeLevelButton.interactable = presentation.CompleteLevelAvailable;
            _resetProgressButton.interactable = presentation.ActionsAvailable;

            _statusText.text = presentation.Status;
            _statusCard.SetActive(!string.IsNullOrWhiteSpace(presentation.Status));
        }
    }
}
#endif
