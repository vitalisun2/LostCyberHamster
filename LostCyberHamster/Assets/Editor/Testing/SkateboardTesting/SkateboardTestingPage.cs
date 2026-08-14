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
        /// Рисует подготовку, ручные команды, сценарии и live status режима.
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
            DrawModeControls();
            EditorGUILayout.Space(8f);
            DrawScenarioControls();
            EditorGUILayout.Space(8f);
            DrawLifecycleControls();
            EditorGUILayout.Space(8f);
            DrawStatus();
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
            using (new EditorGUI.DisabledScope(!_runner.CanPrepare))
            {
                if (GUILayout.Button(
                        "Unlock & Select Skateboard",
                        GUILayout.Width(CommandButtonWidth)))
                {
                    _runner.PrepareUnlockAndSelectSkateboard();
                }
            }
        }

        private void DrawModeControls()
        {
            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_runner.CanEnterMode))
                {
                    if (GUILayout.Button("Enter Mode"))
                        _runner.EnterMode();
                }

                using (new EditorGUI.DisabledScope(!_runner.CanRunScenario))
                {
                    if (GUILayout.Button("Timeout"))
                        _runner.RunTimeoutScenario();
                }
            }

            using (new EditorGUI.DisabledScope(_runner.IsBusy))
            {
                bool useSuperJump = EditorGUILayout.ToggleLeft(
                    "Super Jump",
                    _runner.UseSuperJump);
                if (useSuperJump != _runner.UseSuperJump)
                    _runner.SetUseSuperJump(useSuperJump);
            }
        }

        private void DrawScenarioControls()
        {
            EditorGUILayout.LabelField("Combo scenarios", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_runner.CanRunScenario))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("1 + 1 + 1"))
                        _runner.RunOnePlusOnePlusOneScenario();
                    if (GUILayout.Button("2 + 1"))
                        _runner.RunTwoPlusOneScenario();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("1 + 2"))
                        _runner.RunOnePlusTwoScenario();
                    if (GUILayout.Button("3"))
                        _runner.RunThreeComboScenario();
                }
            }
        }

        private void DrawLifecycleControls()
        {
            EditorGUILayout.LabelField("Lifecycle", EditorStyles.boldLabel);
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

        private void DrawStatus()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_runner.Status, StatusStyle);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
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
