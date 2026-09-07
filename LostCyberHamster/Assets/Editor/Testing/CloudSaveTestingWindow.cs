using System;
using Assets.Scripts.Account;
using Assets.Scripts.Online;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Показывает общий Tools/Testing с доступными testing-страницами.</summary>
    public sealed class CloudSaveTestingWindow : EditorWindow
    {
        private enum TestingPage
        {
            Start,
            CloudSave,
            GameProgress,
            ExperienceProgress,
            Quests,
            Skateboard,
            Skin,
            Resources,
            Networking,
            Account
        }

        /// <summary>Минимальная ширина окна.</summary>
        private const float MinWindowWidth = 520f;

        /// <summary>Минимальная высота окна.</summary>
        private const float MinWindowHeight = 560f;

        /// <summary>Ширина кнопки запуска.</summary>
        private const float TestButtonWidth = 80f;

        /// <summary>Ширина кнопок управления.</summary>
        private const float CommandButtonWidth = 100f;

        /// <summary>Минимальная пауза между автоматическими шагами.</summary>
        private const int MinStepDelaySeconds = 1;

        /// <summary>Ширина поля паузы между автоматическими шагами.</summary>
        private const float StepDelayFieldWidth = 44f;

        /// <summary>Размер текста шага и результата.</summary>
        private const int OutputFontSize = 22;

        /// <summary>Отступ между выводом шага и результата.</summary>
        private const float OutputBlockSpacing = 6f;

        /// <summary>Ширина кнопок выбора продукта.</summary>
        private const float ProductButtonWidth = 190f;

        /// <summary>Высота кнопок выбора продукта.</summary>
        private const float ProductButtonHeight = 34f;

        /// <summary>Выполняет выбранный сценарий.</summary>
        private CloudSaveE2ERunner _runner;

        /// <summary>Рисует и обслуживает страницу Game Progress Testing.</summary>
        private GameProgress.GameProgressTestingPage _gameProgressPage;

        /// <summary>Рисует и обслуживает страницу XP/Level Progress Testing.</summary>
        private ExperienceProgress.ExperienceProgressTestingPage _experienceProgressPage;

        /// <summary>Рисует и обслуживает страницу Quest Testing.</summary>
        private QuestTesting.QuestTestingPage _questTestingPage;

        /// <summary>Рисует и обслуживает страницу Skateboard Mode Testing.</summary>
        private SkateboardTesting.SkateboardTestingPage _skateboardTestingPage;

        /// <summary>Рисует и обслуживает страницу Skin Testing.</summary>
        private SkinTesting.SkinTestingPage _skinTestingPage;

        /// <summary>Рисует и обслуживает страницу Resources.</summary>
        private Resources.ResourcesTestingPage _resourcesTestingPage;

        /// <summary>Общий с DEV-меню экземпляр, обновляемый при смене Play Mode.</summary>
        private GameNetworkFacade _networkFacade;
        private string _networkError;
        private AccountService _testingAccount;
        private string _accountResult;

        /// <summary>Текущая страница общего окна Testing.</summary>
        private TestingPage _currentPage;

        /// <summary>Позиция списка сценариев.</summary>
        private Vector2 _scenarioScrollPosition;

        /// <summary>Стиль крупного текста шага и результата.</summary>
        private GUIStyle _outputStyle;

        /// <summary>Открывает общее окно тестирования.</summary>
        [MenuItem("Tools/Testing", priority = 700)]
        public static void ShowWindow()
        {
            var window = GetWindow<CloudSaveTestingWindow>("Testing");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.BindNetworkFacade();
            window.OnNetworkModeChanged();
            window.Focus();
        }

        /// <summary>Создаёт testing-страницы и подключает единый Play Mode callback.</summary>
        private void OnEnable()
        {
            _runner = new CloudSaveE2ERunner();
            _runner.Changed += Repaint;
            _gameProgressPage = new GameProgress.GameProgressTestingPage(Repaint);
            _experienceProgressPage =
                new ExperienceProgress.ExperienceProgressTestingPage(Repaint);
            _questTestingPage = new QuestTesting.QuestTestingPage(Repaint);
            _skateboardTestingPage =
                new SkateboardTesting.SkateboardTestingPage(Repaint);
            _skinTestingPage = new SkinTesting.SkinTestingPage(Repaint);
            _resourcesTestingPage = new Resources.ResourcesTestingPage(Repaint);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            BindNetworkFacade();
        }

        /// <summary>Освобождает testing-страницы при закрытии окна.</summary>
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (_networkFacade != null)
                _networkFacade.NetworkModeChanged -= OnNetworkModeChanged;
            _networkFacade = null;

            if (_runner != null)
            {
                _runner.Changed -= Repaint;
                if (_runner.IsActive)
                    _runner.Cancel();
            }

            _gameProgressPage?.Dispose();
            _experienceProgressPage?.Dispose();
            _questTestingPage?.Dispose();
            _skateboardTestingPage?.Dispose();
            _skinTestingPage?.Dispose();
            _resourcesTestingPage?.Dispose();
        }

        /// <summary>Рисует текущую страницу окна.</summary>
        private void OnGUI()
        {
            BindNetworkFacade();
            switch (_currentPage)
            {
                case TestingPage.CloudSave:
                    DrawCloudSavePage();
                    break;
                case TestingPage.GameProgress:
                    _gameProgressPage.Draw(() => _currentPage = TestingPage.Start);
                    break;
                case TestingPage.ExperienceProgress:
                    _experienceProgressPage.Draw(
                        () => _currentPage = TestingPage.Start);
                    break;
                case TestingPage.Quests:
                    _questTestingPage.Draw(() => _currentPage = TestingPage.Start);
                    break;
                case TestingPage.Skateboard:
                    _skateboardTestingPage.Draw(
                        () => _currentPage = TestingPage.Start);
                    break;
                case TestingPage.Skin:
                    _skinTestingPage.Draw(() => _currentPage = TestingPage.Start);
                    break;
                case TestingPage.Resources:
                    _resourcesTestingPage.Draw(() => _currentPage = TestingPage.Start);
                    break;
                case TestingPage.Networking:
                    DrawNetworkingPage();
                    break;
                case TestingPage.Account:
                    DrawAccountPage();
                    break;
                default:
                    DrawStartPage();
                    break;
            }
        }

        /// <summary>Рисует список доступных продуктов.</summary>
        private void DrawStartPage()
        {
            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Аккаунт", GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
                _currentPage = TestingPage.Account;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Cloud Save Testing",
                        GUILayout.Width(ProductButtonWidth),
                        GUILayout.Height(ProductButtonHeight)))
                {
                    _currentPage = TestingPage.CloudSave;
                }

                if (GUILayout.Button(
                        "Game Progress Testing",
                        GUILayout.Width(ProductButtonWidth),
                        GUILayout.Height(ProductButtonHeight)))
                {
                    _currentPage = TestingPage.GameProgress;
                }
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button(
                    "XP/Level Progress Testing",
                    GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
            {
                _currentPage = TestingPage.ExperienceProgress;
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button(
                    "Quests",
                    GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
            {
                _currentPage = TestingPage.Quests;
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button(
                    "Skateboard Mode Testing",
                    GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
            {
                _currentPage = TestingPage.Skateboard;
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button(
                    "Skin Testing",
                    GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
            {
                _currentPage = TestingPage.Skin;
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button(
                    "Resources",
                    GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
            {
                _currentPage = TestingPage.Resources;
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button(
                    "Networking",
                    GUILayout.Width(ProductButtonWidth),
                    GUILayout.Height(ProductButtonHeight)))
            {
                _currentPage = TestingPage.Networking;
            }
        }

        /// <summary>Показывает актуальный игровой сервис и общий с DEV чистый старт.</summary>
        private void DrawAccountPage()
        {
            if (GUILayout.Button("Back", GUILayout.Width(70f))) _currentPage = TestingPage.Start;
            EditorGUILayout.LabelField("Аккаунт", EditorStyles.boldLabel);
            var account = EditorApplication.isPlaying && Zenject.ProjectContext.HasInstance
                ? Zenject.ProjectContext.Instance.Container.TryResolve<AccountService>() : null;
            if (!ReferenceEquals(account, _testingAccount))
            {
                _testingAccount = account;
                _accountResult = null;
            }
            EditorGUILayout.HelpBox(
                "Чистый старт заменяет локальный прогресс новым гостевым профилем. " +
                "Настройки сохраняются. Без сети гостевой аккаунт подключится позже.", MessageType.Info);
            EditorGUILayout.LabelField("Состояние", account?.State.ToString() ?? "Запустите Play Mode");

            // Доступность определяется тем же сервисом при каждой перерисовке и смене Play Mode.
            using (new EditorGUI.DisabledScope(account == null || !account.CanStartFreshGuestForTesting))
            {
                if (GUILayout.Button("ЧИСТЫЙ СТАРТ — НОВЫЙ ГОСТЬ"))
                {
                    try
                    {
                        account.StartFreshGuestForTesting();
                        _accountResult = "Новый прогресс готов. Гостевой аккаунт подключится при доступной сети.";
                    }
                    catch (Exception exception)
                    {
                        _accountResult = "Чистый старт не завершён. " + exception.Message;
                        Debug.LogError($"[Account] Fresh start failed: {exception}");
                    }
                    Repaint();
                }
            }
            if (!string.IsNullOrEmpty(_accountResult))
                EditorGUILayout.HelpBox(_accountResult, MessageType.Info);

            // Технические сбросы используют те же методы, что и отдельные действия DEV.
            using (new EditorGUI.DisabledScope(account == null || !account.CanStartFreshGuestForTesting))
            {
                if (GUILayout.Button("RESET LOCAL ACCOUNT STATE"))
                {
                    try
                    {
                        account.ResetLocalAccountStateForTesting();
                        _accountResult = "Локальная сессия очищена. Прогресс сохраняет прежнего владельца.";
                    }
                    catch (Exception exception)
                    {
                        _accountResult = exception.Message;
                        Debug.LogError($"[Account] Local reset failed: {exception}");
                    }
                }
            }
            using (new EditorGUI.DisabledScope(account == null || !account.CanStartFreshGuestForTesting ||
                       !account.TryGetLinkedPlayerId(out _)))
            {
                if (GUILayout.Button("ОТВЯЗАТЬ АККАУНТ И ОЧИСТИТЬ СЕССИЮ"))
                    UnlinkAccountForTesting(account);
            }
        }

        /// <summary>Выполняет серверную отвязку и обновляет результат только для исходного Play Mode.</summary>
        private async void UnlinkAccountForTesting(AccountService account)
        {
            string result;
            try
            {
                await account.FullResetTestAccountAsync();
                result = "Привязка и локальная сессия очищены. Для новой игры используйте чистый старт.";
            }
            catch (OperationCanceledException) { result = "Отвязка отменена."; }
            catch (Exception exception)
            {
                result = exception.Message;
                Debug.LogError($"[Account] Full reset failed: {exception}");
            }

            // Завершение старого запроса не обновляет новое окно или новый игровой контекст.
            if (this != null && EditorApplication.isPlaying && Zenject.ProjectContext.HasInstance &&
                ReferenceEquals(account, Zenject.ProjectContext.Instance.Container.TryResolve<AccountService>()))
            {
                _accountResult = result;
                Repaint();
            }
        }

        /// <summary>Обновляет состояние аккаунта после действий в DEV и фонового входа.</summary>
        private void OnInspectorUpdate()
        {
            if (_currentPage == TestingPage.Account) Repaint();
        }

        /// <summary>Подключает актуальный фасад, включая Play Mode с отключённым Domain Reload.</summary>
        private void BindNetworkFacade()
        {
            var network = GameNetworkFacade.Instance;
            if (ReferenceEquals(_networkFacade, network)) return;
            if (_networkFacade != null)
                _networkFacade.NetworkModeChanged -= OnNetworkModeChanged;
            _networkFacade = network;
            _networkFacade.NetworkModeChanged += OnNetworkModeChanged;
            OnNetworkModeChanged();
        }

        /// <summary>Отражает переключение из любого инструмента и сохраняет видимый индикатор окна.</summary>
        private void OnNetworkModeChanged()
        {
            _networkError = null;
            titleContent = new GUIContent(_networkFacade.IsForcedOffline ? "Testing OFF" : "Testing");
            Repaint();
        }

        /// <summary>Управляет тем же офлайн-режимом, что и runtime DEV, в том числе до запуска игры.</summary>
        private void DrawNetworkingPage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Back", GUILayout.Width(70f)))
                    _currentPage = TestingPage.Start;
                EditorGUILayout.LabelField("Networking", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button(_networkFacade.IsForcedOffline ? "Turn on network" : "Turn off network",
                    GUILayout.Height(ProductButtonHeight)))
            {
                try { _networkFacade.SetForcedOffline(!_networkFacade.IsForcedOffline); }
                catch (Exception exception) { _networkError = $"Не удалось сохранить режим: {exception.Message}"; }
            }

            EditorGUILayout.HelpBox(_networkFacade.IsForcedOffline
                ? "Симуляция офлайна включена"
                : "Сетевые обращения разрешены", MessageType.Info);
            EditorGUILayout.LabelField("Режим сохраняется после перезапуска", EditorStyles.wordWrappedMiniLabel);
            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Можно включить офлайн до Play Mode для проверки запуска игры.", MessageType.Info);
            if (!string.IsNullOrEmpty(_networkError))
                EditorGUILayout.HelpBox(_networkError, MessageType.Error);
        }

        /// <summary>Рисует страницу Cloud Save.</summary>
        private void DrawCloudSavePage()
        {
            DrawCloudSaveHeader();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Cloud Save тесты доступны только в Play Mode. Запустите игру через Bootstrap.",
                    MessageType.Info);
            }

            DrawScenarioList();
            DrawRunnerPanel();
        }

        /// <summary>Рисует заголовок страницы.</summary>
        private void DrawCloudSaveHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_runner.IsActive))
                {
                    if (GUILayout.Button("Back", GUILayout.Width(70f)))
                        _currentPage = TestingPage.Start;
                }

                EditorGUILayout.LabelField("Cloud Save Testing", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(6f);
        }

        /// <summary>Рисует список сценариев.</summary>
        private void DrawScenarioList()
        {
            var listHeight = Mathf.Max(220f, position.height * 0.48f);
            _scenarioScrollPosition = EditorGUILayout.BeginScrollView(
                _scenarioScrollPosition,
                GUILayout.Height(listHeight));

            foreach (var scenario in CloudSaveE2EScenarioCatalog.All)
                DrawScenarioCard(scenario);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Рисует один сценарий.</summary>
        private void DrawScenarioCard(CloudSaveE2EScenario scenario)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    CloudSaveE2EScenarioCatalog.GetTitle(scenario),
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    CloudSaveE2EScenarioCatalog.GetDescription(scenario),
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"Ожидаемый результат: {CloudSaveE2EScenarioCatalog.GetExpectedResult(scenario)}",
                    EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(
                               !EditorApplication.isPlaying || _runner.IsActive))
                    {
                        if (GUILayout.Button("Test", GUILayout.Width(TestButtonWidth)))
                            _runner.Start(scenario);
                    }
                }
            }
        }

        /// <summary>Рисует состояние текущего запуска.</summary>
        private void DrawRunnerPanel()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Текущий тест", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Состояние", GetStateTitle(_runner.State));

            if (_runner.HasScenario)
            {
                EditorGUILayout.LabelField(
                    "Сценарий",
                    CloudSaveE2EScenarioCatalog.GetTitle(_runner.CurrentScenario));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanContinue))
                {
                    if (GUILayout.Button("Continue", GUILayout.Width(CommandButtonWidth)))
                        _runner.Continue();
                }

                using (new EditorGUI.DisabledScope(!_runner.IsActive))
                {
                    if (GUILayout.Button("Cancel", GUILayout.Width(CommandButtonWidth)))
                        _runner.Cancel();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Пауза, сек.", GUILayout.Width(78f));
                _runner.StepDelaySeconds = Mathf.Max(
                    MinStepDelaySeconds,
                    EditorGUILayout.IntField(
                        _runner.StepDelaySeconds,
                        GUILayout.Width(StepDelayFieldWidth)));
            }

            EditorGUILayout.Space(8f);
            var outputStyle = GetOutputStyle();
            var hasStep = !string.IsNullOrWhiteSpace(_runner.CurrentStep);
            if (hasStep)
                DrawOutputBlock("Шаг", _runner.CurrentStep, outputStyle);

            if (!string.IsNullOrWhiteSpace(_runner.CurrentResult))
            {
                if (hasStep)
                    EditorGUILayout.Space(OutputBlockSpacing);

                DrawOutputBlock("Результат", _runner.CurrentResult, outputStyle);
            }
        }

        /// <summary>Рисует многострочный блок вывода с высотой по содержимому.</summary>
        private void DrawOutputBlock(string title, string value, GUIStyle style)
        {
            var content = new GUIContent($"{title}:\n{value}");
            var availableWidth = Mathf.Max(1f, position.width - style.margin.horizontal);
            var height = style.CalcHeight(content, availableWidth);
            EditorGUILayout.LabelField(content, style, GUILayout.Height(height));
        }

        /// <summary>Возвращает стиль крупного вывода теста.</summary>
        private GUIStyle GetOutputStyle()
        {
            if (_outputStyle != null)
                return _outputStyle;

            _outputStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = OutputFontSize,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10)
            };
            _outputStyle.normal.textColor = Color.white;
            return _outputStyle;
        }

        /// <summary>Передаёт смену Play Mode testing-страницам.</summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            BindNetworkFacade();
            if (state == PlayModeStateChange.ExitingPlayMode &&
                _runner != null &&
                _runner.IsActive)
            {
                _runner.Cancel();
            }

            _gameProgressPage?.HandlePlayModeStateChanged(state);
            _experienceProgressPage?.HandlePlayModeStateChanged(state);
            _questTestingPage?.HandlePlayModeStateChanged(state);
            _skateboardTestingPage?.HandlePlayModeStateChanged(state);
            _skinTestingPage?.HandlePlayModeStateChanged(state);
            Repaint();
        }

        /// <summary>Возвращает понятное название состояния.</summary>
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
