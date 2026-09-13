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
        private CloudSaveTestingUiToolkitController _uiToolkitController;

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
        private const float MinWindowWidth = 960f;

        /// <summary>Минимальная высота окна.</summary>
        private const float MinWindowHeight = 900f;

        /// <summary>Ширина кнопки запуска.</summary>
        private const float TestButtonWidth = 180f;

        /// <summary>Ширина кнопок управления.</summary>
        private const float CommandButtonWidth = 220f;

        /// <summary>Минимальная пауза между автоматическими шагами.</summary>
        private const int MinStepDelaySeconds = 1;

        /// <summary>Ширина поля паузы между автоматическими шагами.</summary>
        private const float StepDelayFieldWidth = 88f;

        /// <summary>Размер текста шага и результата.</summary>
        private const int OutputFontSize = 32;

        /// <summary>Отступ между выводом шага и результата.</summary>
        private const float OutputBlockSpacing = 12f;

        /// <summary>Ширина кнопок выбора продукта.</summary>
        private const float ProductButtonWidth = 360f;

        /// <summary>Высота кнопок выбора продукта.</summary>
        private const float ProductButtonHeight = 68f;

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

        public void CreateGUI()
        {
            EnsureUiToolkitController();
        }

        /// <summary>Открывает общее окно тестирования.</summary>
        [MenuItem("Tools/Testing", priority = 700)]
        public static void ShowWindow()
        {
            var window = GetWindow<CloudSaveTestingWindow>("Testing");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            if (window.position.width < MinWindowWidth || window.position.height < MinWindowHeight)
            {
                var rect = window.position;
                window.position = new Rect(
                    rect.x,
                    rect.y,
                    Mathf.Max(rect.width, MinWindowWidth),
                    Mathf.Max(rect.height, MinWindowHeight));
            }
            window.BindNetworkFacade();
            window.OnNetworkModeChanged();
            window.Focus();
        }

        /// <summary>Создаёт testing-страницы и подключает единый Play Mode callback.</summary>
        private void OnEnable()
        {
            EnsureUiToolkitController();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (_uiToolkitController != null)
                return;

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
            if (_uiToolkitController != null)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                _uiToolkitController.Dispose();
                _uiToolkitController = null;
                return;
            }

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
            if (_uiToolkitController != null)
                return;

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
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                EditorGUILayout.LabelField("Testing", TestingWindowLayout.PageTitleStyle);
                EditorGUILayout.LabelField(
                    "Общий каталог testing-разделов. Структура совпадает с runtime DEV, но адаптирована под editor-окно.",
                    TestingWindowLayout.BodyStyle);
                TestingWindowLayout.SpaceSection();

                DrawNavigationCard(
                    "Аккаунт",
                    "Чистый старт, local reset и unlink тестового аккаунта.",
                    () => _currentPage = TestingPage.Account);
                DrawNavigationCard(
                    "Cloud Save Testing",
                    "Сценарии конфликтов и последовательных действий для Cloud Save.",
                    () => _currentPage = TestingPage.CloudSave);
                DrawNavigationCard(
                    "Game Progress Testing",
                    "Подготовка level up и победа по реальному production flow.",
                    () => _currentPage = TestingPage.GameProgress);
                DrawNavigationCard(
                    "XP/Level Progress Testing",
                    "Прогресс уровня, первая сессия и activity/return сценарии.",
                    () => _currentPage = TestingPage.ExperienceProgress);
                DrawNavigationCard(
                    "Quests",
                    "Выбор активного квеста и команды его жизненного цикла.",
                    () => _currentPage = TestingPage.Quests);
                DrawNavigationCard(
                    "Skateboard Mode Testing",
                    "Скриптовые и guided проверки skateboard режима.",
                    () => _currentPage = TestingPage.Skateboard);
                DrawNavigationCard(
                    "Skin Testing",
                    "Unlock, buy и equip следующего скина единым production flow.",
                    () => _currentPage = TestingPage.Skin);
                DrawNavigationCard(
                    "Resources",
                    "Точное DEV-начисление Money и проверка текущего баланса.",
                    () => _currentPage = TestingPage.Resources);
                DrawNavigationCard(
                    "Networking",
                    "Forced offline режим с тем же состоянием, что и в runtime DEV.",
                    () => _currentPage = TestingPage.Networking);
            }
        }

        /// <summary>Показывает актуальный игровой сервис и общий с DEV чистый старт.</summary>
        private void DrawAccountPage()
        {
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawPageHeader("Аккаунт", _runner?.IsActive == true, () => _currentPage = TestingPage.Start);
                var account = EditorApplication.isPlaying && Zenject.ProjectContext.HasInstance
                    ? Zenject.ProjectContext.Instance.Container.TryResolve<AccountService>() : null;
                if (!ReferenceEquals(account, _testingAccount))
                {
                    _testingAccount = account;
                    _accountResult = null;
                }

                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("СОСТОЯНИЕ", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        account?.State.ToString() ?? "Запустите Play Mode",
                        TestingWindowLayout.BodyStyle);
                }

                TestingWindowLayout.SpaceSection();
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("ЧИСТЫЙ СТАРТ", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        "Чистый старт заменяет локальный прогресс новым гостевым профилем. Настройки сохраняются. Без сети гостевой аккаунт подключится позже.",
                        TestingWindowLayout.BodyStyle);
                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(account == null || !account.CanStartFreshGuestForTesting))
                    {
                        if (GUILayout.Button(
                                "ЧИСТЫЙ СТАРТ — НОВЫЙ ГОСТЬ",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(ProductButtonHeight)))
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
                }

                TestingWindowLayout.SpaceSection();
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("LOCAL RESET", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        "Очищает локальную сессию. Текущий прогресс остаётся привязан к прежнему владельцу.",
                        TestingWindowLayout.BodyStyle);
                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(account == null || !account.CanStartFreshGuestForTesting))
                    {
                        if (GUILayout.Button(
                                "RESET LOCAL ACCOUNT STATE",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(ProductButtonHeight)))
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
                }

                TestingWindowLayout.SpaceSection();
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("ОТВЯЗКА ДЛЯ ТЕСТОВ", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        "Удаляет серверную привязку и локальную сессию прежнего аккаунта. Для новой игры используйте чистый старт.",
                        TestingWindowLayout.BodyStyle);
                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(account == null || !account.CanStartFreshGuestForTesting ||
                               !account.TryGetLinkedPlayerId(out _)))
                    {
                        if (GUILayout.Button(
                                "ОТВЯЗАТЬ АККАУНТ И ОЧИСТИТЬ СЕССИЮ",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(ProductButtonHeight)))
                        {
                            UnlinkAccountForTesting(account);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(_accountResult))
                {
                    TestingWindowLayout.SpaceSection();
                    using (TestingWindowLayout.BeginCard())
                    {
                        EditorGUILayout.LabelField("РЕЗУЛЬТАТ", TestingWindowLayout.SectionTitleStyle);
                        EditorGUILayout.LabelField(_accountResult, TestingWindowLayout.BodyStyle);
                    }
                }
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
            if (_uiToolkitController != null)
                return;

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
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawPageHeader("Networking", false, () => _currentPage = TestingPage.Start);

                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("FORCED OFFLINE", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        "Тот же переключатель offline режима, что и в runtime DEV. Состояние сохраняется между перезапусками.",
                        TestingWindowLayout.BodyStyle);
                    EditorGUILayout.Space(8f);
                    if (GUILayout.Button(
                            _networkFacade.IsForcedOffline ? "Turn on network" : "Turn off network",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Height(ProductButtonHeight)))
                    {
                        try { _networkFacade.SetForcedOffline(!_networkFacade.IsForcedOffline); }
                        catch (Exception exception) { _networkError = $"Не удалось сохранить режим: {exception.Message}"; }
                    }
                }

                TestingWindowLayout.SpaceSection();
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("STATUS", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        _networkFacade.IsForcedOffline
                            ? "Симуляция офлайна включена"
                            : "Сетевые обращения разрешены",
                        TestingWindowLayout.BodyStyle);
                    EditorGUILayout.LabelField(
                        "Режим сохраняется после перезапуска",
                        TestingWindowLayout.CaptionStyle);
                    if (!EditorApplication.isPlaying)
                        EditorGUILayout.LabelField(
                            "Можно включить офлайн до Play Mode для проверки запуска игры.",
                            TestingWindowLayout.BodyStyle);
                    if (!string.IsNullOrEmpty(_networkError))
                        EditorGUILayout.LabelField(_networkError, TestingWindowLayout.ErrorStyle);
                }
            }
        }

        private void DrawNavigationCard(string title, string description, Action action)
        {
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField(title, TestingWindowLayout.SectionTitleStyle);
                EditorGUILayout.LabelField(description, TestingWindowLayout.BodyStyle);
                EditorGUILayout.Space(8f);
                if (GUILayout.Button(
                        title,
                        TestingWindowLayout.ButtonStyle,
                        GUILayout.Width(ProductButtonWidth),
                        GUILayout.Height(ProductButtonHeight)))
                {
                    action?.Invoke();
                }
            }

            TestingWindowLayout.SpaceSection();
        }

        private static void DrawPageHeader(string title, bool disableBack, Action navigateBack)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(disableBack))
                {
                    if (GUILayout.Button(
                            "Back",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Width(140f),
                            GUILayout.Height(60f)))
                    {
                        navigateBack?.Invoke();
                    }
                }

                GUILayout.Space(12f);
                EditorGUILayout.LabelField(title, TestingWindowLayout.PageTitleStyle);
            }

            TestingWindowLayout.SpaceSection();
        }

        /// <summary>Рисует страницу Cloud Save.</summary>
        private void DrawCloudSavePage()
        {
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawCloudSaveHeader();

                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        "Cloud Save тесты доступны только в Play Mode. Запустите игру через Bootstrap.",
                        TestingWindowLayout.BodyStyle);
                    TestingWindowLayout.SpaceSection();
                }

                DrawScenarioList();
                DrawRunnerPanel();
            }
        }

        /// <summary>Рисует заголовок страницы.</summary>
        private void DrawCloudSaveHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_runner.IsActive))
                {
                    if (GUILayout.Button(
                            "Back",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Width(140f),
                            GUILayout.Height(60f)))
                        _currentPage = TestingPage.Start;
                }

                GUILayout.Space(12f);
                EditorGUILayout.LabelField("Cloud Save Testing", TestingWindowLayout.PageTitleStyle);
            }

            TestingWindowLayout.SpaceSection();
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
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField(
                    CloudSaveE2EScenarioCatalog.GetTitle(scenario),
                    TestingWindowLayout.SectionTitleStyle);
                EditorGUILayout.LabelField(
                    CloudSaveE2EScenarioCatalog.GetDescription(scenario),
                    TestingWindowLayout.BodyStyle);
                EditorGUILayout.LabelField(
                    $"Ожидаемый результат: {CloudSaveE2EScenarioCatalog.GetExpectedResult(scenario)}",
                    TestingWindowLayout.BodyStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(
                               !EditorApplication.isPlaying || _runner.IsActive))
                    {
                        if (GUILayout.Button(
                                "Test",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Width(TestButtonWidth),
                                GUILayout.Height(ProductButtonHeight)))
                            _runner.Start(scenario);
                    }
                }
            }

            TestingWindowLayout.SpaceSection();
        }

        /// <summary>Рисует состояние текущего запуска.</summary>
        private void DrawRunnerPanel()
        {
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField("ТЕКУЩИЙ ТЕСТ", TestingWindowLayout.SectionTitleStyle);
                EditorGUILayout.LabelField($"Состояние: {GetStateTitle(_runner.State)}", TestingWindowLayout.BodyStyle);

                if (_runner.HasScenario)
                {
                    EditorGUILayout.LabelField(
                        $"Сценарий: {CloudSaveE2EScenarioCatalog.GetTitle(_runner.CurrentScenario)}",
                        TestingWindowLayout.BodyStyle);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!_runner.CanContinue))
                    {
                        if (GUILayout.Button(
                                "Continue",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Width(CommandButtonWidth),
                                GUILayout.Height(ProductButtonHeight)))
                            _runner.Continue();
                    }

                    using (new EditorGUI.DisabledScope(!_runner.IsActive))
                    {
                        if (GUILayout.Button(
                                "Cancel",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Width(CommandButtonWidth),
                                GUILayout.Height(ProductButtonHeight)))
                            _runner.Cancel();
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Пауза, сек.", TestingWindowLayout.CaptionStyle, GUILayout.Width(112f));
                    _runner.StepDelaySeconds = Mathf.Max(
                        MinStepDelaySeconds,
                        EditorGUILayout.IntField(
                            _runner.StepDelaySeconds,
                            GUILayout.Width(StepDelayFieldWidth)));
                }

                EditorGUILayout.Space(12f);
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

            TestingWindowLayout.SpaceSection();
        }

        /// <summary>Рисует многострочный блок вывода с высотой по содержимому.</summary>
        private void DrawOutputBlock(string title, string value, GUIStyle style)
        {
            var content = new GUIContent($"{title}:\n{value}");
            var availableWidth = Mathf.Max(1f, TestingWindowLayout.CurrentContentWidth - style.margin.horizontal);
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
                padding = new RectOffset(18, 18, 16, 16)
            };
            return _outputStyle;
        }

        /// <summary>Передаёт смену Play Mode testing-страницам.</summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (_uiToolkitController != null)
            {
                _uiToolkitController.HandlePlayModeStateChanged(state);
                return;
            }

            BindNetworkFacade();
            if (state == PlayModeStateChange.ExitingPlayMode &&
                _runner != null &&
                _runner.IsActive)
                _runner.Cancel();

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

        private void EnsureUiToolkitController()
        {
            _uiToolkitController ??= new CloudSaveTestingUiToolkitController(this);
        }
    }

    internal static class TestingWindowLayout
    {
        private static GUIStyle _pageTitleStyle;
        private static GUIStyle _sectionTitleStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _captionStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _errorStyle;

        public static GUIStyle PageTitleStyle =>
            _pageTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

        public static GUIStyle SectionTitleStyle =>
            _sectionTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

        public static GUIStyle BodyStyle =>
            _bodyStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 20,
                wordWrap = true
            };

        public static GUIStyle CaptionStyle =>
            _captionStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 16,
                wordWrap = true
            };

        public static GUIStyle ButtonStyle =>
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(18, 18, 14, 14)
            };

        public static GUIStyle ErrorStyle =>
            _errorStyle ??= new GUIStyle(BodyStyle)
            {
                normal = { textColor = new Color(0.72f, 0.15f, 0.12f) }
            };

        public static IDisposable BeginCenteredColumn()
        {
            return new CenteredColumnScope(GetContentWidth());
        }

        public static IDisposable BeginCard()
        {
            return new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        }

        public static void SpaceSection()
        {
            EditorGUILayout.Space(12f);
        }

        public static float CurrentContentWidth => GetContentWidth();

        private static float GetContentWidth()
        {
            float viewWidth = Mathf.Max(400f, EditorGUIUtility.currentViewWidth);
            float maxWidth = viewWidth >= 1200f ? 980f : 760f;
            return Mathf.Max(320f, Mathf.Min(maxWidth, viewWidth - 48f));
        }

        private sealed class CenteredColumnScope : IDisposable
        {
            private readonly EditorGUILayout.HorizontalScope _outerScope;
            private readonly EditorGUILayout.VerticalScope _innerScope;

            public CenteredColumnScope(float width)
            {
                _outerScope = new EditorGUILayout.HorizontalScope();
                GUILayout.FlexibleSpace();
                _innerScope = new EditorGUILayout.VerticalScope(GUILayout.Width(width));
            }

            public void Dispose()
            {
                _innerScope.Dispose();
                GUILayout.FlexibleSpace();
                _outerScope.Dispose();
            }
        }
    }
}
