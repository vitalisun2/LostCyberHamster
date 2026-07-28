#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.ExperienceProgressTesting
{
    /// <summary>Создаёт runtime DEV-представление ручного теста XP и level progress.</summary>
    internal sealed class ExperienceProgressTestingView
    {
        private const int OutputFontSize = 18;

        private readonly GameObject _mainMenuHint;
        private readonly Button _prepareNewRecordButton;
        private readonly Text _prepareNewRecordButtonText;
        private readonly Button _completeNextLevelButton;
        private readonly Text _targetLevelText;
        private readonly Text _statusText;

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
            _mainMenuHint = uiFactory.CreateBodyText(
                "MainMenuHint",
                content,
                "Откройте Main Menu. Тест не переключает текущий экран.")
                .gameObject;
            uiFactory.CreateBodyText(
                "ProgressInfo",
                content,
                "Target остаётся тем же до completion. Prepare берёт реальный weekly best + 10. " +
                "Complete всегда записывает 3 stars; без Prepare использует random score 0–100.");

            // Передаём две команды общему runner через события представления.
            _prepareNewRecordButton = uiFactory.CreateButton(
                "PrepareNewRecordButton",
                content,
                "Prepare New Record",
                DevToolsTheme.Button,
                () => PrepareNewRecordRequested?.Invoke());
            _prepareNewRecordButtonText =
                _prepareNewRecordButton.GetComponentInChildren<Text>();
            _completeNextLevelButton = uiFactory.CreateButton(
                "CompleteNextLevelButton",
                content,
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
        }

        public event Action PrepareNewRecordRequested;

        public event Action CompleteNextLevelRequested;

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
