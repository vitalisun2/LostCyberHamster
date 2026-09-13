#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.ExperienceProgressTesting
{
    /// <summary>Создаёт runtime DEV-представление ручного теста XP и level progress.</summary>
    internal sealed class ExperienceProgressTestingView
    {
        private readonly Assets.Scripts.DevTools.ReturnActivityTesting.ReturnActivityTestingView _returnActivities;
        private const int OutputFontSize = DevToolsTheme.HeadingFontSize + 6;

        private readonly GameObject _mainMenuHint;
        private readonly Button _prepareNewRecordButton;
        private readonly Text _prepareNewRecordButtonText;
        private readonly Button _completeNextLevelButton;
        private readonly Text _targetLevelText;
        private readonly Text _statusText;
        private readonly Button _grantTutorialBonusButton;
        private readonly Button _inspectFirstSessionButton;
        private readonly Text _firstSessionStateText;
        private readonly List<Button> _progressionButtons = new();
        private readonly Text _progressionState;

        public ExperienceProgressTestingView(
            Transform parent,
            DevToolsUiFactory uiFactory)
        {
            RootObject = uiFactory.CreateScrollPage(
                "ExperienceProgressTestingScreen",
                parent,
                out Transform content);

            // Показываем назначение и runtime-условие теста.
            uiFactory.CreateSectionHeading(
                "ExperienceProgressTestingHeading",
                content,
                "XP/LEVEL PROGRESS TESTING");

            Transform overviewCard = uiFactory.CreateCard(
                "ExperienceOverviewCard",
                content,
                DevToolsTheme.Surface);
            uiFactory.CreateSectionHeading("ExperienceOverviewHeading", overviewCard, "FLOW");
            _mainMenuHint = uiFactory.CreateBodyText(
                "MainMenuHint",
                overviewCard,
                "Откройте Main Menu. Тест не переключает текущий экран.")
                .gameObject;
            uiFactory.CreateBodyText(
                "ProgressInfo",
                overviewCard,
                "Target остаётся тем же до completion. Prepare берёт реальный weekly best + 10. " +
                "Complete всегда записывает 3 stars; без Prepare использует random score 0–100.");

            Transform runCard = uiFactory.CreateCard(
                "ExperienceRunCard",
                content,
                DevToolsTheme.Surface);
            uiFactory.CreateSectionHeading("ExperienceRunHeading", runCard, "RUN");
            _prepareNewRecordButton = uiFactory.CreateButton(
                "PrepareNewRecordButton",
                runCard,
                "Prepare New Record",
                DevToolsTheme.Button,
                () => PrepareNewRecordRequested?.Invoke());
            _prepareNewRecordButtonText =
                _prepareNewRecordButton.GetComponentInChildren<Text>();
            _completeNextLevelButton = uiFactory.CreateButton(
                "CompleteNextLevelButton",
                runCard,
                "Complete Next Uncompleted Level",
                DevToolsTheme.Primary,
                () => CompleteNextLevelRequested?.Invoke(),
                DevToolsTheme.PrimaryButtonHeight);

            // Отображаем те же target и status, что Editor Tools/Testing.
            _targetLevelText = CreateOutput(
                uiFactory,
                content,
                "TargetLevel",
                "Target Level");
            _statusText = CreateOutput(
                uiFactory,
                content,
                "Status",
                "Status");

            Transform firstSessionCard = uiFactory.CreateCard(
                "FirstSessionCard",
                content,
                DevToolsTheme.Surface);
            uiFactory.CreateSectionHeading("FirstSessionHeading", firstSessionCard, "FIRST SESSION");
            uiFactory.CreateBodyText(
                "FirstSessionDescription",
                firstSessionCard,
                "Ручная выдача и инспекция первой сессии совпадают с editor Tools/Testing.");
            _grantTutorialBonusButton = uiFactory.CreateButton(
                "GrantTutorialBonusButton", firstSessionCard, "Выдать tutorial-бонус (один раз)",
                DevToolsTheme.Button, () => GrantTutorialBonusRequested?.Invoke());
            _inspectFirstSessionButton = uiFactory.CreateButton(
                "InspectFirstSessionButton", firstSessionCard, "Обновить состояние первой сессии",
                DevToolsTheme.Button, () => InspectFirstSessionRequested?.Invoke());

            Text firstSessionSnapshotHeading = uiFactory.CreateSectionHeading(
                "FirstSessionSnapshotHeading",
                firstSessionCard,
                "SNAPSHOT");
            firstSessionSnapshotHeading.fontSize = DevToolsTheme.ScaleFont(OutputFontSize);
            _firstSessionStateText = uiFactory.CreateBodyText(
                "FirstSessionState",
                firstSessionCard,
                string.Empty);
            _firstSessionStateText.fontSize = DevToolsTheme.ScaleFont(OutputFontSize);

            Transform progressionCard = uiFactory.CreateCard(
                "ProgressionCard",
                content,
                DevToolsTheme.Surface);
            uiFactory.CreateSectionHeading("ProgressionTesting", progressionCard, "PROGRESSION · ИЗОЛИРОВАННЫЙ ПРОФИЛЬ");
            foreach (var command in AbilityProgressTestingRunner.Shared.Commands)
                _progressionButtons.Add(uiFactory.CreateButton("ProgressionCommand" + _progressionButtons.Count,
                    progressionCard, command.Label, DevToolsTheme.Button, () => command.Execute()));
            Text progressionSnapshotHeading = uiFactory.CreateSectionHeading(
                "ProgressionSnapshotHeading",
                progressionCard,
                "SNAPSHOT");
            progressionSnapshotHeading.fontSize = DevToolsTheme.ScaleFont(OutputFontSize);
            _progressionState = uiFactory.CreateBodyText(
                "ProgressionSnapshot",
                progressionCard,
                string.Empty);
            _progressionState.fontSize = DevToolsTheme.ScaleFont(OutputFontSize);
            _returnActivities = new Assets.Scripts.DevTools.ReturnActivityTesting.ReturnActivityTestingView(content, uiFactory);
        }

        public event Action PrepareNewRecordRequested;

        public event Action CompleteNextLevelRequested;
        public event Action GrantTutorialBonusRequested;
        public event Action InspectFirstSessionRequested;

        public GameObject RootObject { get; }

        public void Render(ExperienceProgressTestRunner runner)
        {
            _mainMenuHint.SetActive(!runner.IsMainMenuReady);
            _prepareNewRecordButtonText.text = runner.PrepareNewRecordTitle;
            _prepareNewRecordButton.interactable = runner.CanPrepareNewRecord;
            _completeNextLevelButton.interactable =
                runner.CanCompleteNextLevel;
            _targetLevelText.text = runner.TargetLevel;
            _statusText.text = runner.Status;
            _grantTutorialBonusButton.interactable = runner.CanGrantTutorialBonus;
            _inspectFirstSessionButton.interactable = runner.CanInspectFirstSession;
            _firstSessionStateText.text = runner.FirstSessionState;
            var progression = AbilityProgressTestingRunner.Shared;
            for (int i = 0; i < _progressionButtons.Count; i++)
                _progressionButtons[i].interactable = progression.Commands[i].Enabled();
            _progressionState.text = progression.Snapshot();
            _returnActivities.Render();
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
