using Assets.Scripts.DevTools.ReturnActivityTesting;
using GameManagement;
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
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Активности / Return", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!runner.CanBegin))
                if (GUILayout.Button("Начать изолированную сессию активностей")) runner.Begin();
            using (new EditorGUI.DisabledScope(!GameDataManager.HasProgressionTestingBackup))
                if (GUILayout.Button("Вернуть исходное сохранение")) runner.Restore();
            using (new EditorGUI.DisabledScope(!runner.CanChange))
            {
                if (GUILayout.Button("Победа (без XP)")) runner.Win();
                if (GUILayout.Button("Следующий UTC-день")) runner.NextDay();
                if (GUILayout.Button("Предыдущий UTC-день")) runner.PreviousDay();
                if (GUILayout.Button("+7 UTC-дней")) runner.NextWeek();
                if (GUILayout.Button("Claim одной награды")) runner.Claim();
            }
            if (GUILayout.Button("Прочитать активности")) runner.Inspect();
            EditorGUILayout.HelpBox(runner.Status, MessageType.None);
        }
    }
}
