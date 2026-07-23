using System;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Показывает и запускает пошаговые Cloud Save E2E-сценарии.</summary>
    public sealed class CloudSaveTestingWindow : EditorWindow
    {
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

        /// <summary>Выполняет выбранный сценарий.</summary>
        private CloudSaveE2ERunner _runner;

        /// <summary>Показывает страницу Cloud Save.</summary>
        private bool _showCloudSaveTesting;

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
            window.Focus();
        }

        /// <summary>Подключает окно к runner и Play Mode.</summary>
        private void OnEnable()
        {
            _runner = new CloudSaveE2ERunner();
            _runner.Changed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>Останавливает работу при закрытии окна.</summary>
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (_runner == null)
                return;

            _runner.Changed -= Repaint;
            if (_runner.IsActive)
                _runner.Cancel();
        }

        /// <summary>Рисует текущую страницу окна.</summary>
        private void OnGUI()
        {
            if (_showCloudSaveTesting)
            {
                DrawCloudSavePage();
                return;
            }

            DrawStartPage();
        }

        /// <summary>Рисует список доступных продуктов.</summary>
        private void DrawStartPage()
        {
            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Cloud Save Testing", GUILayout.Height(42f)))
                _showCloudSaveTesting = true;
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
                        _showCloudSaveTesting = false;
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
            if (!string.IsNullOrWhiteSpace(_runner.CurrentStep))
            {
                EditorGUILayout.LabelField(
                    $"Шаг:\n{_runner.CurrentStep}",
                    outputStyle);
            }

            if (!string.IsNullOrWhiteSpace(_runner.CurrentResult))
            {
                EditorGUILayout.LabelField(
                    $"Результат:\n{_runner.CurrentResult}",
                    outputStyle);
            }
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
            return _outputStyle;
        }

        /// <summary>Отменяет тест при выходе из Play Mode.</summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode &&
                _runner != null &&
                _runner.IsActive)
            {
                _runner.Cancel();
            }

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
