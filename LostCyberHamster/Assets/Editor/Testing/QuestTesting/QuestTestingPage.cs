using System;
using Assets.Scripts.DevTools.QuestTesting;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.QuestTesting
{
    /// <summary>Рисует Quest Testing внутри общего окна Tools/Testing.</summary>
    internal sealed class QuestTestingPage : IDisposable
    {
        private const float GenerateButtonWidth = 116f;
        private const float CommandButtonWidth = 96f;

        private readonly Action _repaint;
        private readonly QuestTestRunner _runner;

        /// <summary>Подключает страницу к общему runner квестов.</summary>
        public QuestTestingPage(Action repaint)
        {
            _repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            _runner = QuestTestRunner.Shared;
            _runner.Changed += _repaint;
        }

        /// <summary>Рисует команды реального жизненного цикла первого квеста.</summary>
        public void Draw(Action navigateBack)
        {
            DrawHeader(navigateBack);

            // Показываем требования и влияние команд на реальные данные.
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Тест доступен только в Play Mode. Запустите игру через Bootstrap.",
                    MessageType.Info);
            }
            EditorGUILayout.HelpBox(
                "Generate/Reset сбрасывает реальный MVP-квест и сохраняет состояние. " +
                "Advance/Complete проводят попытку через LevelStarted, " +
                "ObstacleJumpedOver и LevelCompleted. " +
                "Claim Reward вызывает QuestManager.GetReward.",
                MessageType.Info);

            // Рисуем общий статус и одну карточку первого реального квеста.
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_runner.Status, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);
            DrawQuestRow();
        }

        /// <summary>Передаёт shared runner вход и выход из Play Mode.</summary>
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

                EditorGUILayout.LabelField("Quest Testing", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawQuestRow()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_runner.Title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Тип: {_runner.Kind}");
                EditorGUILayout.LabelField($"Статус: {_runner.Status}");
                EditorGUILayout.Space(3f);

                DrawState("До", _runner.BeforeState);
                DrawState("После", _runner.AfterState);
                EditorGUILayout.Space(4f);

                DrawActions();
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawState(string title, string state)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    title,
                    EditorStyles.boldLabel,
                    GUILayout.Width(48f));
                EditorGUILayout.LabelField(state, EditorStyles.wordWrappedLabel);
            }
        }

        private void DrawActions()
        {
            var runnerUnavailable =
                !EditorApplication.isPlaying || !_runner.IsReady || _runner.IsBusy;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           runnerUnavailable || !_runner.CanGenerateOrReset))
                {
                    if (GUILayout.Button(
                            "Generate/Reset",
                            GUILayout.Width(GenerateButtonWidth)))
                    {
                        _runner.GenerateOrReset();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           runnerUnavailable || !_runner.CanAdvance))
                {
                    if (GUILayout.Button(
                            "Advance",
                            GUILayout.Width(CommandButtonWidth)))
                    {
                        _runner.Advance();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           runnerUnavailable || !_runner.CanComplete))
                {
                    if (GUILayout.Button(
                            "Complete",
                            GUILayout.Width(CommandButtonWidth)))
                    {
                        _runner.Complete();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           runnerUnavailable || !_runner.CanClaimReward))
                {
                    if (GUILayout.Button(
                            "Claim Reward",
                            GUILayout.Width(GenerateButtonWidth)))
                    {
                        _runner.ClaimReward();
                    }
                }
            }
        }
    }
}
