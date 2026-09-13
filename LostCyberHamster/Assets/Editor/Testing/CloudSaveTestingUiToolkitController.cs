#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.ExperienceProgressTesting;
using Assets.Scripts.DevTools.GameProgressTesting;
using Assets.Scripts.DevTools.Gameplay;
using Assets.Scripts.DevTools.Networking;
using Assets.Scripts.DevTools.QuestTesting;
using Assets.Scripts.DevTools.SkateboardTesting;
using Assets.Scripts.DevTools.SkinTesting;
using Assets.Scripts.DevTools.UiToolkit;
using Assets.Scripts.Online;
using GameManagement;
using LostCyberHamster.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace LostCyberHamster.Editor.Testing
{
    internal sealed class CloudSaveTestingUiToolkitController : IDisposable
    {
        private readonly CloudSaveTestingWindow _window;
        private readonly DevToolsUiToolkitHost _host;
        private readonly CloudSaveTestingUiPage _cloudSavePage;
        private IVisualElementScheduledItem _refreshSchedule;

        public CloudSaveTestingUiToolkitController(CloudSaveTestingWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _host = new DevToolsUiToolkitHost(showLauncher: false, useSafeArea: false);
            _cloudSavePage = new CloudSaveTestingUiPage(_host.Factory);

            _window.rootVisualElement.Clear();
            _window.rootVisualElement.style.flexGrow = 1f;
            _host.AttachTo(_window.rootVisualElement);

            List<DevToolsNavigationLink> links = new()
            {
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Account, "Аккаунт", "Сессия, чистый старт и отвязка."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.CloudSave, "Cloud Save Testing", "Сценарии конфликтов и последовательных действий для Cloud Save."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Gameplay, "Переключатели", "Бот и временное открытие уровней."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.GameProgress, "Прогресс забега", "Level up и прохождение target-уровней."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.ExperienceProgress, "XP и уровни", "Рекорд, первая сессия и return."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Quests, "Квесты", "Выбор карточки и команды жизненного цикла."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Skateboard, "Скейтборд", "Подготовка, сценарии и guided checks."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Skin, "Скины", "Покупка и экипировка следующего скина."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Resources, "Ресурсы", "Точное начисление Money."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Networking, "Сеть", "Forced offline и сетевой режим."),
            };

            _host.RegisterRootPage(
                DevToolsUiToolkitPageIds.Root,
                new DevToolsNavigationPage(
                    _host.Factory,
                    "Testing Tool",
                    "Та же структура, что и у runtime DEV, но в editor-окне и с Cloud Save страницей.",
                    links,
                    _host.Navigate));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Account, new AccountDevToolsUiPage(_host.Factory, ResolveAccountService));
            _host.RegisterPage(DevToolsUiToolkitPageIds.CloudSave, _cloudSavePage);
            _host.RegisterPage(DevToolsUiToolkitPageIds.Gameplay, new GameplayDevToolsUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.GameProgress, new GameProgressTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.ExperienceProgress, new ExperienceProgressTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Quests, new QuestTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Skateboard, new SkateboardTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Skin, new SkinTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Resources, new ResourcesDevToolsUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Networking, new NetworkingDevToolsUiPage(_host.Factory, () => GameNetworkFacade.Instance));

            _refreshSchedule = _window.rootVisualElement.schedule.Execute(Refresh).Every(100);
            Refresh();
        }

        public void Refresh()
        {
            GameProgressTestRunner.Shared.Tick();
            SkateboardTestingRunner.Shared.Tick();
            _host.Refresh();
        }

        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                GameProgressTestRunner.Shared.HandlePlayModeStopped();
                ExperienceProgressTestRunner.Shared.HandlePlayModeStopped();
                QuestTestRunner.Shared.HandlePlayModeStopped();
                SkateboardTestingRunner.Shared.HandlePlayModeStopped();
                SkinTestingRunner.Shared.ResetStatus();
                _cloudSavePage.CancelIfActive();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                GameProgressTestRunner.Shared.HandlePlayModeStarted();
                ExperienceProgressTestRunner.Shared.HandlePlayModeStarted();
                QuestTestRunner.Shared.HandlePlayModeStarted();
                SkateboardTestingRunner.Shared.HandlePlayModeStarted();
                SkinTestingRunner.Shared.ResetStatus();
            }

            Refresh();
        }

        public void Dispose()
        {
            _refreshSchedule?.Pause();
            _refreshSchedule = null;
            _cloudSavePage.CancelIfActive();
            _host.Dispose();
        }

        private static AccountService ResolveAccountService()
        {
            return EditorApplication.isPlaying && ProjectContext.HasInstance
                ? ProjectContext.Instance.Container.TryResolve<AccountService>()
                : null;
        }
    }

    internal sealed class CloudSaveTestingUiPage : DevToolsUiPageBase
    {
        private readonly CloudSaveE2ERunner _runner = new();
        private readonly List<Button> _scenarioButtons = new();
        private readonly IntegerField _stepDelayField;
        private readonly Button _continueButton;
        private readonly Button _cancelButton;
        private readonly Label _stateLabel;
        private readonly Label _scenarioLabel;
        private readonly Label _stepLabel;
        private readonly Label _resultLabel;

        public CloudSaveTestingUiPage(DevToolsUiToolkitFactory factory)
            : base(factory, "Cloud Save Testing")
        {
            Root.Add(factory.CreateBody(
                "Cloud Save тесты доступны только в Play Mode. Запустите игру через Bootstrap и выполняйте сценарии по шагам."));

            foreach (CloudSaveE2EScenario scenario in CloudSaveE2EScenarioCatalog.All)
            {
                VisualElement card = factory.CreateCard(
                    CloudSaveE2EScenarioCatalog.GetTitle(scenario),
                    CloudSaveE2EScenarioCatalog.GetDescription(scenario) + "\nОжидаемый результат: " + CloudSaveE2EScenarioCatalog.GetExpectedResult(scenario));
                Button button = factory.CreateActionButton(
                    "Test",
                    DevToolsUiToolkitTheme.SurfaceAccent,
                    () => _runner.Start(scenario),
                    compact: true);
                card.Add(button);
                Root.Add(card);
                _scenarioButtons.Add(button);
            }

            VisualElement runCard = factory.CreateCard("ТЕКУЩИЙ ТЕСТ", background: DevToolsUiToolkitTheme.SurfaceAccent);
            _stateLabel = factory.CreateStatus(string.Empty);
            _scenarioLabel = factory.CreateStatus(string.Empty);
            _stepLabel = factory.CreateStatus(string.Empty);
            _resultLabel = factory.CreateStatus(string.Empty);
            _stepDelayField = factory.CreateIntegerField("Пауза, сек.");
            _stepDelayField.RegisterValueChangedCallback(evt =>
            {
                _runner.StepDelaySeconds = Mathf.Max(1, evt.newValue);
            });
            _continueButton = factory.CreateActionButton("Continue", DevToolsUiToolkitTheme.Surface, _runner.Continue, compact: true);
            _cancelButton = factory.CreateActionButton("Cancel", DevToolsUiToolkitTheme.SurfaceDanger, _runner.Cancel, compact: true);
            runCard.Add(_stateLabel);
            runCard.Add(_scenarioLabel);
            runCard.Add(_stepDelayField);
            runCard.Add(_continueButton);
            runCard.Add(_cancelButton);
            runCard.Add(_stepLabel);
            runCard.Add(_resultLabel);
            Root.Add(runCard);
        }

        public override void Refresh()
        {
            bool canStart = EditorApplication.isPlaying && !_runner.IsActive;
            foreach (Button button in _scenarioButtons)
                button.SetEnabled(canStart);

            _continueButton.SetEnabled(_runner.CanContinue);
            _cancelButton.SetEnabled(_runner.IsActive);
            _stepDelayField.SetValueWithoutNotify(_runner.StepDelaySeconds);
            _stateLabel.text = $"Состояние: {GetStateTitle(_runner.State)}";
            _scenarioLabel.text = _runner.HasScenario
                ? $"Сценарий: {CloudSaveE2EScenarioCatalog.GetTitle(_runner.CurrentScenario)}"
                : "Сценарий не выбран.";
            _stepLabel.text = string.IsNullOrWhiteSpace(_runner.CurrentStep)
                ? "Шаги появятся после запуска сценария."
                : "Шаг:\n" + _runner.CurrentStep;
            _resultLabel.text = string.IsNullOrWhiteSpace(_runner.CurrentResult)
                ? "Результат появится после выполнения шага."
                : "Результат:\n" + _runner.CurrentResult;
        }

        public void CancelIfActive()
        {
            if (_runner.IsActive)
                _runner.Cancel();
        }

        private static string GetStateTitle(CloudSaveE2ERunState state)
        {
            return state switch
            {
                CloudSaveE2ERunState.Idle => "Не запущен",
                CloudSaveE2ERunState.Running => "Выполняется",
                CloudSaveE2ERunState.WaitingForUser => "Ожидает действия",
                CloudSaveE2ERunState.Passed => "Пройден",
                CloudSaveE2ERunState.Failed => "Ошибка",
                CloudSaveE2ERunState.Cancelled => "Отменён",
                _ => state.ToString()
            };
        }
    }
}
#endif