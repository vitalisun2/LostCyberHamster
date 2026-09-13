using System;
using Assets.Scripts.DevTools.ExperienceProgressTesting;
using LostCyberHamster.Editor.Testing;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.ExperienceProgress
{
    /// <summary>Рисует XP/Level Progress Testing внутри общего окна Tools/Testing.</summary>
    internal sealed class ExperienceProgressTestingPage : IDisposable
    {
        private const int CommandButtonHeight = 68;
        private const int OutputFontSize = 28;
        private const int OutputHeadingFontSize = 24;

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

        /// <summary>Рисует команды прогресса, tutorial-бонус и снимок состояния первой сессии.</summary>
        public void Draw(Action navigateBack)
        {
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawHeader(navigateBack);
                ReturnActivityTestingPage.Draw();

                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        "Тест доступен только в Play Mode. Запустите игру через Bootstrap.",
                        TestingWindowLayout.BodyStyle);
                    TestingWindowLayout.SpaceSection();
                }
                else if (!_runner.IsMainMenuReady)
                {
                    EditorGUILayout.LabelField(
                        "Откройте Main Menu. Тест не переключает текущий экран.",
                        TestingWindowLayout.BodyStyle);
                    TestingWindowLayout.SpaceSection();
                }

                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("FLOW", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        "Target остаётся тем же до completion. Prepare берёт реальный weekly best + 10. Complete всегда записывает 3 stars; без Prepare использует random score 0–100.",
                        TestingWindowLayout.BodyStyle);
                }

                TestingWindowLayout.SpaceSection();
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("RUN", TestingWindowLayout.SectionTitleStyle);
                    using (new EditorGUI.DisabledScope(!_runner.CanPrepareNewRecord))
                    {
                        if (GUILayout.Button(
                                _runner.PrepareNewRecordTitle,
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(CommandButtonHeight)))
                        {
                            _runner.PrepareNewRecord();
                        }
                    }

                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(!_runner.CanCompleteNextLevel))
                    {
                        if (GUILayout.Button(
                                "Complete Next Uncompleted Level",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(CommandButtonHeight)))
                        {
                            _runner.CompleteNextLevel();
                        }
                    }
                }

                TestingWindowLayout.SpaceSection();
                DrawOutputSection("Target Level", _runner.TargetLevel, OutputTextStyle);
                DrawOutputSection("Status", _runner.Status, OutputTextStyle);

                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("FIRST SESSION", TestingWindowLayout.SectionTitleStyle);
                    using (new EditorGUI.DisabledScope(!_runner.CanGrantTutorialBonus))
                    {
                        if (GUILayout.Button(
                                "Выдать tutorial-бонус (один раз)",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(CommandButtonHeight)))
                        {
                            _runner.GrantTutorialBonus();
                        }
                    }

                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(!_runner.CanInspectFirstSession))
                    {
                        if (GUILayout.Button(
                                "Обновить состояние первой сессии",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(CommandButtonHeight)))
                        {
                            _runner.InspectFirstSessionState();
                        }
                    }

                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("SNAPSHOT", OutputHeadingStyle);
                    EditorGUILayout.LabelField(_runner.FirstSessionState, OutputTextStyle);
                }

                TestingWindowLayout.SpaceSection();
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("PROGRESSION · ИЗОЛИРОВАННЫЙ ПРОФИЛЬ", TestingWindowLayout.SectionTitleStyle);
                    var progression = AbilityProgressTestingRunner.Shared;
                    foreach (var command in progression.Commands)
                    {
                        using (new EditorGUI.DisabledScope(!command.Enabled()))
                        {
                            if (GUILayout.Button(
                                    command.Label,
                                    TestingWindowLayout.ButtonStyle,
                                    GUILayout.Height(60f)))
                            {
                                command.Execute();
                            }
                        }

                        EditorGUILayout.Space(8f);
                    }

                    EditorGUILayout.LabelField("SNAPSHOT", OutputHeadingStyle);
                    EditorGUILayout.LabelField(progression.Snapshot(), OutputTextStyle);
                }
            }
        }

        /// <summary>Обновляет статус страницы после входа или выхода из Play Mode.</summary>
        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                AbilityProgressTestingRunner.Shared.EndSession();
                _runner.HandlePlayModeStopped();
            }
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
                    if (GUILayout.Button(
                            "Back",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Width(140f),
                            GUILayout.Height(60f)))
                        navigateBack?.Invoke();
                }

                EditorGUILayout.LabelField(
                    "XP/Level Progress Testing",
                    TestingWindowLayout.PageTitleStyle);
            }

            TestingWindowLayout.SpaceSection();
        }

        private void DrawOutputSection(
            string heading,
            string value,
            GUIStyle valueStyle)
        {
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField(heading, OutputHeadingStyle);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(value, valueStyle);
            }

            TestingWindowLayout.SpaceSection();
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
