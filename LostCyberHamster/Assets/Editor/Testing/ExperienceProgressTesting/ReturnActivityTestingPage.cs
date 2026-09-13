using Assets.Scripts.DevTools.ReturnActivityTesting;
using GameManagement;
using LostCyberHamster.Editor.Testing;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor.Testing.ExperienceProgress
{
    /// <summary>Те же ручные production-команды, что на DEV-странице, без отдельного тестового каркаса.</summary>
    internal static class ReturnActivityTestingPage
    {
        public static void Draw()
        {
            var runner = ReturnActivityTestingRunner.Shared;

            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField("АКТИВНОСТИ / RETURN", TestingWindowLayout.SectionTitleStyle);
                EditorGUILayout.LabelField(
                    "Те же production-команды активностей, что и в runtime DEV, но в вертикальном editor layout.",
                    TestingWindowLayout.BodyStyle);

                EditorGUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(!runner.CanBegin))
                    if (GUILayout.Button(
                            "Начать изолированную сессию активностей",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Height(68f))) runner.Begin();
                EditorGUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(!GameDataManager.HasProgressionTestingBackup))
                    if (GUILayout.Button(
                            "Вернуть исходное сохранение",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Height(68f))) runner.Restore();
            }

            TestingWindowLayout.SpaceSection();
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField("MUTATIONS", TestingWindowLayout.SectionTitleStyle);
                using (new EditorGUI.DisabledScope(!runner.CanChange))
                {
                    if (GUILayout.Button("Победа (без XP)", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f))) runner.Win();
                    EditorGUILayout.Space(8f);
                    if (GUILayout.Button("Следующий UTC-день", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f))) runner.NextDay();
                    EditorGUILayout.Space(8f);
                    if (GUILayout.Button("Предыдущий UTC-день", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f))) runner.PreviousDay();
                    EditorGUILayout.Space(8f);
                    if (GUILayout.Button("+7 UTC-дней", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f))) runner.NextWeek();
                    EditorGUILayout.Space(8f);
                    if (GUILayout.Button("Claim одной награды", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f))) runner.Claim();
                }
            }

            TestingWindowLayout.SpaceSection();
            using (TestingWindowLayout.BeginCard())
            {
                EditorGUILayout.LabelField("SNAPSHOT", TestingWindowLayout.SectionTitleStyle);
                if (GUILayout.Button("Прочитать активности", TestingWindowLayout.ButtonStyle, GUILayout.Height(68f))) runner.Inspect();
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField(runner.Status, TestingWindowLayout.BodyStyle);
            }

            TestingWindowLayout.SpaceSection();
        }
    }
}
