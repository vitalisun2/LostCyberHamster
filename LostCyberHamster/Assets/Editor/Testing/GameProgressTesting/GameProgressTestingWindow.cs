using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.GameProgress
{
    /// <summary>Показывает пошаговый ручной тест игрового прогресса.</summary>
    public sealed class GameProgressTestingWindow : EditorWindow
    {
        private const float MinWindowWidth = 560f;
        private const float MinWindowHeight = 520f;
        private const float CommandButtonWidth = 170f;
        private const float LogMinHeight = 220f;

        private GameProgressTestRunner _runner;
        private Vector2 _logScrollPosition;
        private int _lastLogCount;

        /// <summary>Открывает отдельное окно тестирования игрового прогресса.</summary>
        public static void ShowWindow()
        {
            var window = GetWindow<GameProgressTestingWindow>("Game Progress Testing");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Focus();
        }

        /// <summary>Восстанавливает runner и подписки окна.</summary>
        private void OnEnable()
        {
            _runner = new GameProgressTestRunner();
            _runner.Changed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>Освобождает transient-операции, сохраняя checkpoint сессии.</summary>
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (_runner == null)
                return;

            _runner.Changed -= Repaint;
            _runner.Dispose();
        }

        /// <summary>Рисует управление, текущую точку и журнал действий.</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Game Progress Testing", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Тест доступен только в Play Mode. Запустите игру через Bootstrap.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "Новый прогон сбрасывает локальный игровой прогресс. Cancel не восстанавливает старый save.",
                MessageType.Warning);

            DrawCommands();
            DrawStatus();
            DrawLog();
        }

        private void DrawCommands()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanUsePrimaryAction))
                {
                    if (GUILayout.Button(
                            _runner.PrimaryActionTitle,
                            GUILayout.Width(CommandButtonWidth),
                            GUILayout.Height(34f)))
                    {
                        _runner.RunPrimaryAction();
                    }
                }

                using (new EditorGUI.DisabledScope(!_runner.CanCancel))
                {
                    if (GUILayout.Button(
                            "Cancel",
                            GUILayout.Width(90f),
                            GUILayout.Height(34f)))
                    {
                        _runner.Cancel();
                    }
                }
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Текущая точка", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_runner.CurrentPoint, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Статус", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_runner.Status, EditorStyles.wordWrappedLabel);
        }

        private void DrawLog()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Log действий", EditorStyles.boldLabel);

            _logScrollPosition = EditorGUILayout.BeginScrollView(
                _logScrollPosition,
                GUILayout.MinHeight(LogMinHeight));

            var log = _runner.Log;
            foreach (var line in log)
                EditorGUILayout.SelectableLabel(line, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndScrollView();

            if (_lastLogCount != log.Count)
            {
                _lastLogCount = log.Count;
                _logScrollPosition.y = float.MaxValue;
                Repaint();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            _runner?.HandlePlayModeStateChanged(state);
            Repaint();
        }
    }
}
