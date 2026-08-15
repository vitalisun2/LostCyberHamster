using System;
using Assets.Scripts.DevTools.SkateboardTesting;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.SkateboardTesting
{
    /// <summary>
    /// Рисует ручные проверки Skateboard внутри общего окна Tools/Testing.
    /// </summary>
    internal sealed class SkateboardTestingPage : IDisposable
    {
        private const float CommandButtonWidth = 210f;
        private const int StatusFontSize = 14;

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
            DrawHeader(navigateBack);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Skateboard Testing доступен только в Play Mode.",
                    MessageType.Info);
            }

            DrawPreparation();
            EditorGUILayout.Space(8f);
            DrawScriptedComboScenarios();
            EditorGUILayout.Space(8f);
            DrawGuidedBehaviorChecks();
            EditorGUILayout.Space(8f);
            DrawLiveStatus();
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
            if (_runner.IsBusy)
                _runner.Cancel();
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
                    "Skateboard Testing",
                    EditorStyles.boldLabel);
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
                            GUILayout.Width(CommandButtonWidth)))
                    {
                        _runner.PrepareUnlockAndSelectSkateboard();
                    }
                }

                using (new EditorGUI.DisabledScope(!_runner.CanEnterMode))
                {
                    if (GUILayout.Button("Enter Mode"))
                        _runner.EnterMode();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanPause))
                {
                    if (GUILayout.Button("Pause"))
                        _runner.Pause();
                }

                using (new EditorGUI.DisabledScope(!_runner.CanResume))
                {
                    if (GUILayout.Button("Resume"))
                        _runner.Resume();
                }

                using (new EditorGUI.DisabledScope(!_runner.CanCancel))
                {
                    if (GUILayout.Button("Cancel"))
                        _runner.Cancel();
                }
            }
        }

        private void DrawScriptedComboScenarios()
        {
            EditorGUILayout.LabelField(
                "Scripted Combo Scenarios",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_runner.CanRunScenario))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool useSuperJump = EditorGUILayout.ToggleLeft(
                        "Super Jump",
                        _runner.UseSuperJump);
                    if (useSuperJump != _runner.UseSuperJump)
                        _runner.SetUseSuperJump(useSuperJump);

                    bool onRoof = EditorGUILayout.ToggleLeft(
                        "On Roof",
                        _runner.OnRoof);
                    if (onRoof != _runner.OnRoof)
                        _runner.SetOnRoof(onRoof);
                }
            }

            using (new EditorGUI.DisabledScope(!_runner.CanRunScenario))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("1+1+1"))
                        _runner.RunOnePlusOnePlusOneScenario();
                    if (GUILayout.Button("2+1"))
                        _runner.RunTwoPlusOneScenario();
                    if (GUILayout.Button("1+2"))
                        _runner.RunOnePlusTwoScenario();
                    if (GUILayout.Button("3 Combo"))
                        _runner.RunThreeComboScenario();
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
                    if (GUILayout.Button("Timeout (automatic)"))
                        _runner.RunTimeoutCheck();
                    if (GUILayout.Button("Ride Damage"))
                        _runner.StartRideDamageCheck();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Jump Collision"))
                        _runner.StartJumpCollisionCheck();
                    if (GUILayout.Button("Lane Shift"))
                        _runner.StartLaneShiftCheck();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanStopCheck))
                {
                    if (GUILayout.Button("Stop Check"))
                        _runner.StopCheck();
                }

                using (new EditorGUI.DisabledScope(!_runner.CanCancel))
                {
                    if (GUILayout.Button("Cancel"))
                        _runner.Cancel();
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
            _statusStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = StatusFontSize,
                wordWrap = true
            };
    }
}
