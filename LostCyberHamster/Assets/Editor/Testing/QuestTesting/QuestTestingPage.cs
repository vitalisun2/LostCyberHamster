using System;
using Assets.Scripts.DevTools.QuestTesting;
using LostCyberHamster.Editor.Testing;
using UnityEditor;
using UnityEngine;
using Vues.GameCore.Quests;

namespace LostCyberHamster.Editor.Testing.QuestTesting
{
    /// <summary>Рисует Quest Testing внутри общего окна Tools/Testing.</summary>
    internal sealed class QuestTestingPage : IDisposable
    {
        private static readonly string[] _categoryNames =
            { "Daily", "Story" };

        private const float HeaderButtonWidth = 140f;
        private const float SectionSpacing = 12f;
        private const float CardSpacing = 8f;
        private const float SelectorHeight = 44f;
        private const float ActionButtonHeight = 60f;
        private const float StateCardMinHeight = 116f;

        private readonly Action _repaint;
        private readonly QuestTestRunner _runner;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _captionStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _stateStyle;

        /// <summary>Подключает страницу к общему runner квестов.</summary>
        public QuestTestingPage(Action repaint)
        {
            _repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            _runner = QuestTestRunner.Shared;
            _runner.Changed += _repaint;
            GameEventsManager.OnDailyQuestSetChanged +=
                _runner.HandleDailyQuestSetChanged;
            GameEventsManager.OnStoryQuestSetChanged +=
                _runner.HandleStoryQuestSetChanged;
        }

        /// <summary>Рисует выбор квеста и команды его реального жизненного цикла.</summary>
        public void Draw(Action navigateBack)
        {
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawHeader(navigateBack);

                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        "Тест доступен только в Play Mode. Запустите игру через Bootstrap.",
                        TestingWindowLayout.BodyStyle);
                    TestingWindowLayout.SpaceSection();
                }

                EditorGUILayout.LabelField(
                    "Only selected quest gets progress. +1 Quest Day shifts only quest time and lets QuestManager react through the normal daily check.",
                    TestingWindowLayout.BodyStyle);
                TestingWindowLayout.SpaceSection();

                DrawQuestDayCard();
                EditorGUILayout.Space(SectionSpacing);
                DrawQuestSelectionCard();
                EditorGUILayout.Space(SectionSpacing);
                DrawStatusCard();
                EditorGUILayout.Space(SectionSpacing);
                DrawStateCards();
                EditorGUILayout.Space(SectionSpacing);
                DrawActionCard();
            }
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
            GameEventsManager.OnDailyQuestSetChanged -=
                _runner.HandleDailyQuestSetChanged;
            GameEventsManager.OnStoryQuestSetChanged -=
                _runner.HandleStoryQuestSetChanged;
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
                            GUILayout.Width(HeaderButtonWidth),
                            GUILayout.Height(60f)))
                        navigateBack?.Invoke();
                }

                GUILayout.Space(12f);
                EditorGUILayout.LabelField("Quest Testing", TestingWindowLayout.PageTitleStyle);
            }

            TestingWindowLayout.SpaceSection();
        }

        private void DrawQuestDayCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Quest Day", SectionTitleStyle);
                EditorGUILayout.Space(CardSpacing);
                DrawInfoRow("Device", _runner.DeviceLocalTime);
                DrawInfoRow("Quest", _runner.QuestLocalTime);
                DrawInfoRow("Mode", _runner.QuestTimeMode);
                DrawInfoRow("Daily", _runner.DailyGenerationDate);
                DrawInfoRow("Story", _runner.StoryGenerationDate);
                EditorGUILayout.Space(CardSpacing);

                using (new EditorGUI.DisabledScope(
                           !_runner.CanSimulateNextQuestDay))
                {
                    if (GUILayout.Button(
                            "+1 Quest Day",
                            GUILayout.Height(ActionButtonHeight)))
                    {
                        _runner.SimulateNextQuestDay();
                    }
                }
            }
        }

        private void DrawQuestSelectionCard()
        {
            int categoryIndex =
                _runner.SelectedCategory == QuestCategory.Daily ? 0 : 1;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Quest", SectionTitleStyle);
                EditorGUILayout.Space(CardSpacing);
                EditorGUILayout.LabelField("Category", CaptionStyle);

                using (new EditorGUI.DisabledScope(_runner.IsBusy))
                {
                    int selectedCategory = GUILayout.Toolbar(
                        categoryIndex,
                        _categoryNames,
                        GUILayout.Height(SelectorHeight));
                    if (selectedCategory != categoryIndex)
                    {
                        _runner.SelectCategory(
                            selectedCategory == 0
                                ? QuestCategory.Daily
                                : QuestCategory.Story);
                    }

                    EditorGUILayout.Space(CardSpacing);
                    string[] questOptions = _runner.GetQuestOptions();
                    if (questOptions.Length == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "Нет активных квестов в выбранной категории.",
                            MessageType.Info);
                        return;
                    }

                    EditorGUILayout.LabelField("Active Quest", CaptionStyle);
                    int selectedQuest = EditorGUILayout.Popup(
                        _runner.SelectedQuestIndex,
                        questOptions,
                        GUILayout.Height(SelectorHeight));
                    if (selectedQuest != _runner.SelectedQuestIndex)
                    {
                        _runner.SelectQuest(selectedQuest);
                    }
                }

                EditorGUILayout.Space(CardSpacing);
                EditorGUILayout.LabelField(_runner.Title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_runner.Kind, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawStatusCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Status", SectionTitleStyle);
                EditorGUILayout.Space(CardSpacing);
                EditorGUILayout.LabelField(_runner.Status, StatusStyle);
            }
        }

        private void DrawStateCards()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStateCard("Before", _runner.BeforeState);
                DrawStateCard("After", _runner.AfterState);
            }
        }

        private void DrawStateCard(string title, string value)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.MinHeight(StateCardMinHeight),
                       GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(title, SectionTitleStyle);
                EditorGUILayout.Space(CardSpacing);
                EditorGUILayout.LabelField(value, StateStyle);
            }
        }

        private void DrawActionCard()
        {
            bool runnerUnavailable =
                !EditorApplication.isPlaying || !_runner.IsReady || _runner.IsBusy;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Actions", SectionTitleStyle);
                EditorGUILayout.Space(CardSpacing);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(
                               runnerUnavailable || !_runner.CanResetQuest))
                    {
                        if (GUILayout.Button(
                                "Reset Quest",
                                GUILayout.Height(ActionButtonHeight)))
                        {
                            _runner.ResetQuest();
                        }
                    }

                    using (new EditorGUI.DisabledScope(
                               runnerUnavailable || !_runner.CanAdvance))
                    {
                        if (GUILayout.Button(
                                "Advance",
                                GUILayout.Height(ActionButtonHeight)))
                        {
                            _runner.Advance();
                        }
                    }

                    using (new EditorGUI.DisabledScope(
                               runnerUnavailable || !_runner.CanComplete))
                    {
                        if (GUILayout.Button(
                                "Complete",
                                GUILayout.Height(ActionButtonHeight)))
                        {
                            _runner.Complete();
                        }
                    }

                    using (new EditorGUI.DisabledScope(
                               runnerUnavailable || !_runner.CanClaimReward))
                    {
                        if (GUILayout.Button(
                                "Claim Reward",
                                GUILayout.Height(ActionButtonHeight)))
                        {
                            _runner.ClaimReward();
                        }
                    }
                }
            }
        }

        private void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    label,
                    CaptionStyle,
                    GUILayout.Width(52f));
                EditorGUILayout.LabelField(
                    value,
                    EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(2f);
        }

        private GUIStyle SectionTitleStyle =>
            _sectionTitleStyle ??= new GUIStyle(TestingWindowLayout.SectionTitleStyle);

        private GUIStyle CaptionStyle =>
            _captionStyle ??= new GUIStyle(TestingWindowLayout.CaptionStyle)
            {
                alignment = TextAnchor.MiddleLeft
            };

        private GUIStyle StatusStyle =>
            _statusStyle ??= new GUIStyle(TestingWindowLayout.BodyStyle)
            {
                wordWrap = true
            };

        private GUIStyle StateStyle =>
            _stateStyle ??= new GUIStyle(TestingWindowLayout.BodyStyle)
            {
                wordWrap = true
            };
    }
}
