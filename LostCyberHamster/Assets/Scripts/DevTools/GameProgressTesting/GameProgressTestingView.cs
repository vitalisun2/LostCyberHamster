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
        private const int OutputFontSize = DevToolsTheme.HeadingFontSize + 6;

        private readonly Button _prepareLevelUpButton;
        private readonly Button _resetProgressButton;
        private readonly Button _winCurrentLevelButton;
        private readonly Button _winCurrentPartOfDayButton;
        private readonly Button _winCurrentLocationButton;
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
                "GameProgressTestingDescription",
                content,
                "Те же production-flow команды, что и в Tools/Testing, но сгруппированы по карточкам с крупными CTA.");

            _prepareLevelUpButton = CreateCommandCard(
                uiFactory,
                content,
                "PrepareLevelUp",
                "Prepare Level Up",
                "Sets XP to 239/240. The next XP reward shows the Level Up modal.",
                DevToolsTheme.Button,
                () => PrepareLevelUpRequested?.Invoke());
            _resetProgressButton = CreateCommandCard(
                uiFactory,
                content,
                "ResetProgress",
                "Reset Progress",
                "Resets all local player progress.",
                DevToolsTheme.Danger,
                () => ResetProgressRequested?.Invoke());
            _winCurrentLevelButton = CreateCommandCard(
                uiFactory,
                content,
                "WinCurrentLevel",
                "Win Current Level",
                "Uses the running level, or opens PlayerData.CurrentLevel. Finishes with 3 stars and a random score.",
                DevToolsTheme.Primary,
                () => WinCurrentLevelRequested?.Invoke());
            _winCurrentPartOfDayButton = CreateCommandCard(
                uiFactory,
                content,
                "WinCurrentPartOfDay",
                "Win Current Part of Day",
                "Wins every remaining level in the current part of day through the real result flow. Uses a 0.3 s modal delay.",
                DevToolsTheme.Primary,
                () => WinCurrentPartOfDayRequested?.Invoke());
            _winCurrentLocationButton = CreateCommandCard(
                uiFactory,
                content,
                "WinCurrentLocation",
                "Win Current Location",
                "Wins every remaining level in the current location through the real result flow. Uses a 0.1 s modal delay.",
                DevToolsTheme.Primary,
                () => WinCurrentLocationRequested?.Invoke());

            _currentTargetText = CreateOutput(
                uiFactory,
                content,
                "CurrentTarget",
                "Current Level");
            _statusText = CreateOutput(
                uiFactory,
                content,
                "Status",
                "Status");
            _currentActionText = CreateOutput(
                uiFactory,
                content,
                "CurrentAction",
                "Last Action");
        }

        public event Action PrepareLevelUpRequested;
        public event Action ResetProgressRequested;
        public event Action WinCurrentLevelRequested;
        public event Action WinCurrentPartOfDayRequested;
        public event Action WinCurrentLocationRequested;

        public GameObject RootObject { get; }

        public void Render(GameProgressTestRunner runner)
        {
            _prepareLevelUpButton.interactable = runner.CanPrepareLevelUp;
            _resetProgressButton.interactable = runner.CanResetProgress;
            _winCurrentLevelButton.interactable = runner.CanWinCurrentLevel;
            _winCurrentPartOfDayButton.interactable =
                runner.CanWinCurrentPartOfDay;
            _winCurrentLocationButton.interactable =
                runner.CanWinCurrentLocation;
            _currentTargetText.text = runner.CurrentPoint;
            _statusText.text = runner.Status;
            _currentActionText.text = runner.CurrentAction;
        }

        /// <summary>Создаёт карточку команды с описанием и крупной CTA-кнопкой.</summary>
        private static Button CreateCommandCard(
            DevToolsUiFactory uiFactory,
            Transform parent,
            string name,
            string buttonLabel,
            string description,
            Color buttonColor,
            Action action)
        {
            Transform card = uiFactory.CreateCard(
                $"{name}Card",
                parent,
                DevToolsTheme.Surface);
            uiFactory.CreateSectionHeading($"{name}Heading", card, buttonLabel);
            Text descriptionText = uiFactory.CreateBodyText(
                $"{name}Description",
                card,
                description);
            descriptionText.alignment = TextAnchor.UpperLeft;

            Button button = uiFactory.CreateButton(
                $"{name}Button",
                card,
                buttonLabel,
                buttonColor,
                () => action?.Invoke());
            Text buttonText = button.GetComponentInChildren<Text>();
            buttonText.resizeTextForBestFit = true;
            buttonText.resizeTextMinSize = DevToolsTheme.ScaleFont(18);
            buttonText.resizeTextMaxSize = DevToolsTheme.ScaleFont(DevToolsTheme.ButtonFontSize);
            return button;
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
            headingText.fontSize = DevToolsTheme.ScaleFont(OutputFontSize);

            Text valueText = uiFactory.CreateBodyText(
                $"{name}Text",
                card,
                string.Empty);
            valueText.fontSize = DevToolsTheme.ScaleFont(OutputFontSize);
            return valueText;
        }
    }
}
#endif
