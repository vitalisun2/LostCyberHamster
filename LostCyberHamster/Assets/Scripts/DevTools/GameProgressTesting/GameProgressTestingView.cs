#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.GameProgressTesting
{
    /// <summary>Создаёт runtime DEV-представление ручного теста игрового прогресса.</summary>
    internal sealed class GameProgressTestingView
    {
        private const int OutputFontSize = 18;

        private readonly Button _primaryButton;
        private readonly Text _primaryButtonText;
        private readonly Button _cancelButton;
        private readonly Button _resetProgressButton;
        private readonly Text _currentTargetText;
        private readonly Text _statusText;
        private readonly Text _currentActionText;

        public GameProgressTestingView(Transform parent, DevToolsUiFactory uiFactory)
        {
            RootObject = uiFactory.CreateScrollPage(
                "GameProgressTestingScreen",
                parent,
                out Transform content);

            uiFactory.CreateSectionHeading(
                "GameProgressTestingHeading",
                content,
                "GAME PROGRESS TESTING");
            uiFactory.CreateBodyText(
                "ProgressWarning",
                content,
                "Start and Reset Progress reset real local progress. Level results use the normal game save flow.");

            _primaryButton = uiFactory.CreateButton(
                "PrimaryButton",
                content,
                "Start",
                DevToolsTheme.Primary,
                () => PrimaryRequested?.Invoke(),
                DevToolsTheme.PrimaryButtonHeight);
            _primaryButtonText = _primaryButton.GetComponentInChildren<Text>();
            _cancelButton = uiFactory.CreateButton(
                "CancelButton",
                content,
                "Cancel",
                DevToolsTheme.Button,
                () => CancelRequested?.Invoke());
            _resetProgressButton = uiFactory.CreateButton(
                "ResetProgressButton",
                content,
                "Reset Progress",
                DevToolsTheme.Danger,
                () => ResetProgressRequested?.Invoke());

            _currentTargetText = CreateOutput(
                uiFactory,
                content,
                "CurrentTarget",
                "Current Target");
            _statusText = CreateOutput(
                uiFactory,
                content,
                "Status",
                "Status");
            _currentActionText = CreateOutput(
                uiFactory,
                content,
                "CurrentAction",
                "Current Action");
        }

        public event Action PrimaryRequested;
        public event Action CancelRequested;
        public event Action ResetProgressRequested;

        public GameObject RootObject { get; }

        public void Render(GameProgressTestRunner runner)
        {
            _primaryButtonText.text = runner.PrimaryActionTitle;
            _primaryButton.interactable = runner.CanUsePrimaryAction;
            _cancelButton.interactable = runner.CanCancel;
            _resetProgressButton.interactable = runner.CanResetProgress;
            _currentTargetText.text = runner.CurrentPoint;
            _statusText.text = runner.Status;
            _currentActionText.text = runner.CurrentAction;
        }

        private static Text CreateOutput(
            DevToolsUiFactory uiFactory,
            Transform parent,
            string name,
            string heading)
        {
            Transform card = uiFactory.CreateCard(
                $"{name}Card",
                parent,
                DevToolsTheme.StatusCard);
            Text headingText = uiFactory.CreateSectionHeading(
                $"{name}Heading",
                card,
                heading);
            headingText.fontStyle = FontStyle.Bold;
            headingText.fontSize = OutputFontSize;

            Text valueText = uiFactory.CreateBodyText(
                $"{name}Text",
                card,
                string.Empty);
            valueText.fontSize = OutputFontSize;
            return valueText;
        }
    }
}
#endif
