using System;
using Assets.Scripts.DevTools.ExperienceProgressTesting;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.ExperienceProgress
{
    /// <summary>Рисует XP/Level Progress Testing внутри общего окна Tools/Testing.</summary>
    internal sealed class ExperienceProgressTestingPage : IDisposable
    {
        private const float CommandButtonWidth = 260f;
        private const int OutputFontSize = 16;
        private const int OutputHeadingFontSize = 20;

        private readonly Action _repaint;
        private readonly ExperienceProgressTestRunner _runner;
        private GUIStyle _outputTextStyle;
        private GUIStyle _outputHeadingStyle;

        public ExperienceProgressTestingPage(Action repaint)
        {
            _repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            _runner = ExperienceProgressTestRunner.Shared;
            _runner.Changed += _repaint;
        }

        /// <summary>Рисует подготовку рекорда, completion, текущую цель и результат.</summary>
        public void Draw(Action navigateBack)
        {
            DrawHeader(navigateBack);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Тест доступен только в Play Mode. Запустите игру через Bootstrap.",
                    MessageType.Info);
            }
            else if (!_runner.IsMainMenuReady)
            {
                EditorGUILayout.HelpBox(
                    "Откройте Main Menu. Тест не переключает текущий экран.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "Target остаётся тем же до completion. Prepare берёт реальный weekly best + 10. " +
                "Complete всегда записывает 3 stars; без Prepare использует random score 0–100.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(
                       !_runner.CanPrepareNewRecord))
            {
                if (GUILayout.Button(
                        _runner.PrepareNewRecordTitle,
                        GUILayout.Width(CommandButtonWidth),
                        GUILayout.Height(34f)))
                {
                    _runner.PrepareNewRecord();
                }
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!_runner.CanCompleteNextLevel))
            {
                if (GUILayout.Button(
                        "Complete Next Uncompleted Level",
                        GUILayout.Width(CommandButtonWidth),
                        GUILayout.Height(34f)))
                {
                    _runner.CompleteNextLevel();
                }
            }

            EditorGUILayout.Space(8f);
            DrawOutputSection(
                "Target Level",
                _runner.TargetLevel,
                OutputTextStyle);
            DrawOutputSection(
                "Status",
                _runner.Status,
                OutputTextStyle);
        }

        /// <summary>Обновляет статус страницы после входа или выхода из Play Mode.</summary>
        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                _runner.HandlePlayModeStopped();
            else if (state == PlayModeStateChange.EnteredPlayMode)
                _runner.HandlePlayModeStarted();
        }

        /// <summary>Отписывает Editor-страницу от shared runner.</summary>
        public void Dispose()
        {
            _runner.Changed -= _repaint;
        }

        private void DrawHeader(Action navigateBack)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_runner.IsBusy))
                {
                    if (GUILayout.Button("Back", GUILayout.Width(70f)))
                        navigateBack?.Invoke();
                }

                EditorGUILayout.LabelField(
                    "XP/Level Progress Testing",
                    EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawOutputSection(
            string heading,
            string value,
            GUIStyle valueStyle)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(heading, OutputHeadingStyle);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(value, valueStyle);
            }

            EditorGUILayout.Space(4f);
        }

        private GUIStyle OutputTextStyle =>
            _outputTextStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = OutputFontSize,
                wordWrap = true
            };

        private GUIStyle OutputHeadingStyle =>
            _outputHeadingStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = OutputHeadingFontSize,
                fontStyle = FontStyle.Bold
            };
    }
}
