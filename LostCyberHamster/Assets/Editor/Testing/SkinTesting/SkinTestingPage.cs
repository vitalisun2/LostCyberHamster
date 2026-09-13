using System;
using Assets.Scripts.DevTools.SkinTesting;
using LostCyberHamster.Editor.Testing;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.SkinTesting
{
    /// <summary>
    /// Рисует покупку и применение следующего скина внутри Tools/Testing.
    /// </summary>
    internal sealed class SkinTestingPage : IDisposable
    {
        private const float ActionButtonWidth = 360f;

        private readonly Action _repaint;
        private readonly SkinTestingRunner _runner;

        /// <summary>Подключает страницу к общему runner.</summary>
        public SkinTestingPage(Action repaint)
        {
            _repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            _runner = SkinTestingRunner.Shared;
            _runner.Changed += _repaint;
        }

        /// <summary>Рисует одну команду и её компактный результат.</summary>
        public void Draw(Action navigateBack)
        {
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                DrawHeader(navigateBack);

                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("FLOW", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(_runner.AvailabilityStatus, TestingWindowLayout.BodyStyle);
                    EditorGUILayout.Space(8f);

                    using (new EditorGUI.DisabledScope(!_runner.CanRun))
                    {
                        if (GUILayout.Button(
                                "Unlock, Buy & Equip Next Skin",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Width(ActionButtonWidth),
                                GUILayout.Height(68f)))
                        {
                            _runner.UnlockBuyAndEquipNextSkin();
                        }
                    }
                }

                EditorGUILayout.Space(12f);
                DrawStatus();
            }
        }

        /// <summary>Передаёт runner вход и выход из Play Mode.</summary>
        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                _runner.ResetStatus();
            }
        }

        /// <summary>Отписывает страницу от runner.</summary>
        public void Dispose()
        {
            _runner.Changed -= _repaint;
        }

        private static void DrawHeader(Action navigateBack)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Back", GUILayout.Width(70f)))
                    navigateBack?.Invoke();

                GUILayout.Space(12f);
                EditorGUILayout.LabelField("Skin Testing", TestingWindowLayout.PageTitleStyle);
            }
        }

        private void DrawStatus()
        {
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField("Status", TestingWindowLayout.SectionTitleStyle);
                EditorGUILayout.LabelField(_runner.Status, TestingWindowLayout.BodyStyle);
                EditorGUILayout.LabelField("Target", _runner.TargetStatus);
                EditorGUILayout.LabelField("Price", _runner.PriceStatus);
                EditorGUILayout.LabelField("Granted", _runner.GrantedStatus);
                EditorGUILayout.LabelField("Purchase", _runner.PurchaseStatus);
                EditorGUILayout.LabelField("Applied", _runner.AppliedStatus);
            }
        }
    }
}
