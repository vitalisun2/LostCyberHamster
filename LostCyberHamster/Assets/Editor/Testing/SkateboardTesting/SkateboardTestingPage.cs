using System;
using Assets.Scripts.DevTools.SkateboardTesting;
using LostCyberHamster.Editor.Testing;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.SkateboardTesting
{
    /// <summary>
    /// Рисует ручные проверки Skateboard внутри общего окна Tools/Testing.
    /// </summary>
    internal sealed class SkateboardTestingPage : IDisposable
    {
        private const float CommandButtonWidth = 360f;
        private const int StatusFontSize = 20;

        private readonly Action _repaint;
        private readonly SkateboardTestingRunner _runner;
        private GUIStyle _statusStyle;

        /// <summary>
        /// Подключает страницу к общему runner и editor update loop.
        /// </summary>
        public SkateboardTestingPage(Action repaint)
        {
            _repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            _runner = SkateboardTestingRunner.Shared;
            _runner.Changed += _repaint;
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>
        /// Рисует preparation, scripted scenarios, guided checks и live status.
        /// </summary>
        public void Draw(Action navigateBack)
        {
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawHeader(navigateBack);

                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        "Skateboard Testing доступен только в Play Mode.",
                        TestingWindowLayout.BodyStyle);
                    TestingWindowLayout.SpaceSection();
                }

                DrawPreparation();
                EditorGUILayout.Space(12f);
                DrawScriptedScenarios();
                EditorGUILayout.Space(12f);
                DrawGuidedBehaviorChecks();
                EditorGUILayout.Space(12f);
                DrawLiveStatus();
            }
        }

        /// <summary>
        /// Передаёт runner вход и выход из Play Mode.
        /// </summary>
        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                _runner.HandlePlayModeStopped();
            else if (state == PlayModeStateChange.EnteredPlayMode)
                _runner.HandlePlayModeStarted();
        }

        /// <summary>
        /// Отписывает страницу от runner и editor update loop.
        /// </summary>
        public void Dispose()
        {
            EditorApplication.update -= OnEditorUpdate;
            _runner.Changed -= _repaint;
            if (_runner.CanStopCheck)
                _runner.StopCheck();
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
                    "Skateboard Testing",
                        TestingWindowLayout.PageTitleStyle);
            }
        }

        private void DrawPreparation()
        {
            EditorGUILayout.LabelField("Preparation", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanPrepare))
                {
                    if (GUILayout.Button(
                            "Unlock & Select Skateboard",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Width(CommandButtonWidth),
                            GUILayout.Height(68f)))
                    {
                        _runner.PrepareUnlockAndSelectSkateboard();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanTogglePause))
                {
                    if (GUILayout.Button(_runner.PauseButtonLabel, TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.TogglePause();
                }

                using (new EditorGUI.DisabledScope(!_runner.CanStopCheck))
                {
                    if (GUILayout.Button("Stop Check", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.StopCheck();
                }
            }
        }

        private void DrawScriptedScenarios()
        {
            EditorGUILayout.LabelField(
                "Scripted Scenarios",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_runner.CanRunScenario))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Jump", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.RunJumpScenario();
                    if (GUILayout.Button("Super Jump", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.RunSuperJumpScenario();
                }
            }
        }

        private void DrawGuidedBehaviorChecks()
        {
            EditorGUILayout.LabelField(
                "Guided Behavior Checks",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_runner.CanStartGuidedCheck))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Timeout (automatic)", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.RunTimeoutCheck();
                    if (GUILayout.Button("Ride Collision", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.StartRideCollisionCheck();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Jump Collision", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.StartJumpCollisionCheck();
                    if (GUILayout.Button("Lane Shift", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f)))
                        _runner.StartLaneShiftCheck();
                }
            }

            if (!string.IsNullOrEmpty(_runner.Instruction))
                EditorGUILayout.HelpBox(_runner.Instruction, MessageType.Info);
            DrawChecklist();
        }

        private void DrawChecklist()
        {
            foreach (SkateboardTestingRunner.ChecklistItem item in
                     _runner.Checklist)
            {
                Color color;
                string prefix;
                switch (item.State)
                {
                    case SkateboardTestingRunner.ChecklistState.Pass:
                        color = new Color(0.2f, 0.75f, 0.25f);
                        prefix = "PASS";
                        break;
                    case SkateboardTestingRunner.ChecklistState.Fail:
                        color = new Color(0.9f, 0.25f, 0.2f);
                        prefix = "FAIL";
                        break;
                    default:
                        color = Color.gray;
                        prefix = "[ ]";
                        break;
                }

                Color previousColor = GUI.contentColor;
                GUI.contentColor = color;
                string details = string.IsNullOrEmpty(item.Details)
                    ? string.Empty
                    : $" — {item.Details}";
                EditorGUILayout.LabelField(
                    $"{prefix} {item.Label}{details}",
                    StatusStyle);
                GUI.contentColor = previousColor;
            }
        }

        private void DrawLiveStatus()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Live Status", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_runner.Status, StatusStyle);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(_runner.LiveStatus, StatusStyle);
            }
        }

        private void OnEditorUpdate()
        {
            _runner.Tick();
            if (EditorApplication.isPlaying)
                _repaint();
        }

        private GUIStyle StatusStyle =>
            _statusStyle ??= new GUIStyle(TestingWindowLayout.BodyStyle)
            {
                fontSize = StatusFontSize,
                wordWrap = true
            };
    }
}
