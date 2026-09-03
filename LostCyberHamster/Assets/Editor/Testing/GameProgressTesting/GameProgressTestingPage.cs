using System;
using Assets.Scripts.DevTools.GameProgressTesting;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.GameProgress
{
    /// <summary>Рисует Game Progress Testing внутри общего окна Tools/Testing.</summary>
    internal sealed class GameProgressTestingPage : IDisposable
    {
        private const float CommandButtonWidth = 180f;
        private const float CommandRowHeight = 42f;
        private const int OutputFontSize = 16;
        private const int OutputHeadingFontSize = 20;

        private readonly Action _repaint;
        private readonly GameProgressTestRunner _runner;
        private GUIStyle _commandDescriptionStyle;
        private GUIStyle _outputTextStyle;
        private GUIStyle _outputHeadingStyle;

        public GameProgressTestingPage(Action repaint)
        {
            _repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            _runner = GameProgressTestRunner.Shared;
            _runner.Changed += _repaint;
            EditorApplication.update += TickRunner;
        }

        /// <summary>Рисует страницу, команды, текущий уровень, статус и последнее действие.</summary>
        public void Draw(Action navigateBack)
        {
            DrawHeader(navigateBack);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Тест доступен только в Play Mode. Запустите игру через Bootstrap.",
                    MessageType.Info);
            }

            DrawCommands();
            DrawStatus();
        }

        /// <summary>Передаёт shared runner вход и выход из Play Mode.</summary>
        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                _runner.HandlePlayModeStopped();
            else if (state == PlayModeStateChange.EnteredPlayMode)
                _runner.HandlePlayModeStarted();
        }

        /// <summary>Отписывает Editor-адаптер, не завершая shared runner и не очищая его контекст.</summary>
        public void Dispose()
        {
            EditorApplication.update -= TickRunner;
            _runner.Changed -= _repaint;
        }

        private void TickRunner()
        {
            _runner.Tick();
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

                EditorGUILayout.LabelField("Game Progress Testing", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawCommands()
        {
            DrawCommandRow(
                "Prepare Level Up",
                "Sets XP to 239/240. The next XP reward shows the Level Up modal.",
                _runner.CanPrepareLevelUp,
                _runner.PrepareLevelUp);
            DrawCommandRow(
                "Reset Progress",
                "Resets all local player progress.",
                _runner.CanResetProgress,
                _runner.ResetProgress);
            DrawCommandRow(
                "Win Current Level",
                "Uses the running level, or opens PlayerData.CurrentLevel. Finishes with 3 stars and a random score.",
                _runner.CanWinCurrentLevel,
                _runner.WinCurrentLevel);
            DrawCommandRow(
                "Win Current Part of Day",
                "Wins every remaining level in the current part of day through the real result flow. Uses a 0.3 s modal delay.",
                _runner.CanWinCurrentPartOfDay,
                _runner.WinCurrentPartOfDay);
            DrawCommandRow(
                "Win Current Location",
                "Wins every remaining level in the current location through the real result flow. Uses a 0.1 s modal delay.",
                _runner.CanWinCurrentLocation,
                _runner.WinCurrentLocation);
        }

        /// <summary>Рисует одну команду с фиксированной кнопкой и кратким описанием.</summary>
        private void DrawCommandRow(
            string buttonTitle,
            string description,
            bool isEnabled,
            Action action)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!isEnabled))
                {
                    if (GUILayout.Button(
                            buttonTitle,
                            GUILayout.Width(CommandButtonWidth),
                            GUILayout.Height(CommandRowHeight)))
                    {
                        action?.Invoke();
                    }
                }

                EditorGUILayout.LabelField(
                    description,
                    CommandDescriptionStyle,
                    GUILayout.Height(CommandRowHeight));
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawStatus()
        {
            EditorGUILayout.Space(8f);
            DrawOutputSection(
                "Current Level",
                FormatCurrentTarget(_runner.CurrentPoint));
            DrawOutputSection("Status", _runner.Status);
            DrawOutputSection("Last Action", _runner.CurrentAction);
        }

        private static string FormatCurrentTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return string.Empty;

            var readableTarget = target.Trim();
            var technicalSuffixIndex = readableTarget.LastIndexOf(
                " (",
                StringComparison.Ordinal);
            if (technicalSuffixIndex > 0 &&
                readableTarget.EndsWith(")", StringComparison.Ordinal))
            {
                readableTarget = readableTarget.Substring(0, technicalSuffixIndex);
            }

            readableTarget = readableTarget.Replace(" / level ", " / Level ");
            var addressParts = readableTarget.Split('/');
            if (addressParts.Length != 3 || readableTarget.Contains(" / "))
                return readableTarget;

            var locationName = FormatIdentifier(addressParts[0], skipNumericPrefix: true);
            var partName = FormatIdentifier(addressParts[1], skipNumericPrefix: false);
            var levelName = FormatIdentifier(addressParts[2], skipNumericPrefix: false);
            return $"{locationName} / {partName} / {levelName}";
        }

        private static string FormatIdentifier(
            string identifier,
            bool skipNumericPrefix)
        {
            var words = identifier.Trim().Split('_');
            var firstWordIndex = skipNumericPrefix &&
                                 words.Length > 1 &&
                                 int.TryParse(words[0], out _)
                ? 1
                : 0;

            for (var index = firstWordIndex; index < words.Length; index++)
            {
                if (int.TryParse(words[index], out var number))
                    words[index] = number.ToString();
                else if (index == firstWordIndex && words[index].Length > 0)
                    words[index] = char.ToUpperInvariant(words[index][0]) +
                                   words[index].Substring(1);
            }

            return string.Join(" ", words, firstWordIndex, words.Length - firstWordIndex);
        }

        private void DrawOutputSection(string heading, string value)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(heading, OutputHeadingStyle);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(value, OutputTextStyle);
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

        private GUIStyle CommandDescriptionStyle =>
            _commandDescriptionStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
    }
}
