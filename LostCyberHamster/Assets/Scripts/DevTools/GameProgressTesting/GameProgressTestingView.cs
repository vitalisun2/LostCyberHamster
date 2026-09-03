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
        private const float CommandButtonWidth = 180f;
        private const int OutputFontSize = 18;

        private readonly Button _prepareLevelUpButton;
        private readonly Button _resetProgressButton;
        private readonly Button _winCurrentLevelWithRandomValuesButton;
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

            _prepareLevelUpButton = CreateCommandRow(
                uiFactory,
                content,
                "PrepareLevelUp",
                "Prepare Level Up",
                "Sets XP to 239/240. The next XP reward shows the Level Up modal.",
                DevToolsTheme.Button,
                () => PrepareLevelUpRequested?.Invoke());
            _resetProgressButton = CreateCommandRow(
                uiFactory,
                content,
                "ResetProgress",
                "Reset Progress",
                "Resets all local player progress.",
                DevToolsTheme.Danger,
                () => ResetProgressRequested?.Invoke());
            _winCurrentLevelWithRandomValuesButton = CreateCommandRow(
                uiFactory,
                content,
                "WinCurrentLevelWithRandomValues",
                "Win with Random",
                "Uses the running level, or opens PlayerData.CurrentLevel. Finishes with 3 stars and a random score.",
                DevToolsTheme.Primary,
                () => WinCurrentLevelWithRandomValuesRequested?.Invoke());

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
        public event Action WinCurrentLevelWithRandomValuesRequested;

        public GameObject RootObject { get; }

        public void Render(GameProgressTestRunner runner)
        {
            _prepareLevelUpButton.interactable = runner.CanPrepareLevelUp;
            _resetProgressButton.interactable = runner.CanResetProgress;
            _winCurrentLevelWithRandomValuesButton.interactable =
                runner.CanWinCurrentLevelWithRandomValues;
            _currentTargetText.text = runner.CurrentPoint;
            _statusText.text = runner.Status;
            _currentActionText.text = runner.CurrentAction;
        }

        /// <summary>Создаёт строку с фиксированной кнопкой и кратким описанием.</summary>
        private static Button CreateCommandRow(
            DevToolsUiFactory uiFactory,
            Transform parent,
            string name,
            string buttonLabel,
            string description,
            Color buttonColor,
            Action action)
        {
            GameObject rowObject = new GameObject(
                $"{name}Row",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            rowObject.transform.SetParent(parent, false);

            HorizontalLayoutGroup rowLayout =
                rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = DevToolsTheme.ContentSpacing;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            ContentSizeFitter rowFitter = rowObject.GetComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button button = uiFactory.CreateButton(
                $"{name}Button",
                rowObject.transform,
                buttonLabel,
                buttonColor,
                () => action?.Invoke());
            LayoutElement buttonLayout = button.GetComponent<LayoutElement>();
            buttonLayout.minWidth = CommandButtonWidth;
            buttonLayout.preferredWidth = CommandButtonWidth;
            buttonLayout.flexibleWidth = 0f;

            Text descriptionText = uiFactory.CreateBodyText(
                $"{name}Description",
                rowObject.transform,
                description);
            descriptionText.alignment = TextAnchor.MiddleLeft;
            LayoutElement descriptionLayout =
                descriptionText.gameObject.AddComponent<LayoutElement>();
            descriptionLayout.flexibleWidth = 1f;
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
