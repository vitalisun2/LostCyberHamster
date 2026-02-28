#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameManagement;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Editor tool for launching test levels directly from the menu.
    /// Sets the current level key and enters Play mode in the Game scene.
    /// 
    /// Prerequisites (manual setup):
    /// 1. Car sprites (car_1, car_2, car_3) must be marked as Addressable with label "new_york_obstacles_sprites"
    /// 2. Test level JSON must be marked as Addressable with the correct level key
    /// 3. Medium roof .anim clips need to be in Assets/Animations/Hamster/
    /// </summary>
    public static class TestLevelLauncher
    {
        private const string TestLevelKey = "test_medium_notalive";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string TestLevelJsonPath = "Assets/Content/locations/01_New_York/levels/Test/test_medium_notalive/test_medium_notalive.json";

        [MenuItem("Tools/Test Level/Launch Medium NotAlive Test", priority = 50)]
        private static void LaunchTestLevel()
        {
            // Verify test level JSON exists
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(TestLevelJsonPath);
            if (json == null)
            {
                Debug.LogError($"[TestLevelLauncher] Test level JSON not found at: {TestLevelJsonPath}");
                return;
            }

            // Verify Game scene exists
            if (!System.IO.File.Exists(GameScenePath))
            {
                Debug.LogError($"[TestLevelLauncher] Game scene not found at: {GameScenePath}");
                return;
            }

            // Set the current level
            if (GameDataManager.PlayerData != null)
            {
                GameDataManager.PlayerData.CurrentLevel = TestLevelKey;
                GameDataManager.Save();
                Debug.Log($"[TestLevelLauncher] Set CurrentLevel to '{TestLevelKey}'. Entering Play mode...");
            }
            else
            {
                Debug.LogWarning("[TestLevelLauncher] GameDataManager.PlayerData is null. " +
                    "Setting PlayerPrefs fallback. Run the game once first to initialize PlayerData.");
            }

            // Open Game scene and enter Play mode
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(GameScenePath);
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem("Tools/Test Level/Open Test Level JSON", priority = 51)]
        private static void OpenTestLevelJson()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TestLevelJsonPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
            else
            {
                Debug.LogError($"[TestLevelLauncher] Test level JSON not found at: {TestLevelJsonPath}");
            }
        }

        [MenuItem("Tools/Test Level/Setup Checklist", priority = 52)]
        private static void ShowSetupChecklist()
        {
            Debug.Log(
                "[TestLevelLauncher] Setup Checklist:\n" +
                "1. Mark car sprites as Addressable:\n" +
                "   - obstacle_new_york_car_1.png (label: new_york_obstacles_sprites)\n" +
                "   - obstacle_new_york_car_2.png (label: new_york_obstacles_sprites)\n" +
                "   - obstacle_new_york_car_3.png (label: new_york_obstacles_sprites)\n" +
                "2. Mark test level JSON as Addressable:\n" +
                "   - test_medium_notalive.json (address: test_medium_notalive)\n" +
                "3. Verify medium .anim clips exist in Assets/Animations/Hamster/\n" +
                "4. Run 'Tools > Test Level > Launch Medium NotAlive Test'"
            );
        }
    }
}
#endif
