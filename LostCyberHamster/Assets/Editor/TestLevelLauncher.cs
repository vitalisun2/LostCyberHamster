#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Editor tool for launching test levels directly from the menu.
    /// Stores the test level JSON path in SessionState and enters Play mode.
    /// LevelDataProvider reads the override and loads JSON directly from disk,
    /// bypassing Addressables entirely for test levels.
    /// </summary>
    public static class TestLevelLauncher
    {
        /// <summary>SessionState key used by LevelDataProvider to detect test override.</summary>
        public const string OverrideSessionKey = "TestLevel_JsonPath";

        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string TestLevelJsonPath =
            "Assets/Content/locations/01_New_York/levels/Test/test_medium_notalive/test_medium_notalive.json";

        [MenuItem("Tools/Test Level/Launch Medium NotAlive Test", priority = 50)]
        private static void LaunchTestLevel()
        {
            try
            {
                // Verify test level JSON exists
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>(TestLevelJsonPath);
                if (json == null)
                {
                    EditorUtility.DisplayDialog("Test Level",
                        $"Test level JSON not found:\n{TestLevelJsonPath}", "OK");
                    return;
                }

                // Verify Game scene exists
                if (!System.IO.File.Exists(GameScenePath))
                {
                    EditorUtility.DisplayDialog("Test Level",
                        $"Game scene not found:\n{GameScenePath}", "OK");
                    return;
                }

                if (EditorApplication.isPlaying)
                {
                    EditorUtility.DisplayDialog("Test Level",
                        "Exit Play mode first before launching a test level.", "OK");
                    return;
                }

                // Store override path — LevelDataProvider will pick it up at runtime
                SessionState.SetString(OverrideSessionKey, TestLevelJsonPath);

                Debug.Log($"[TestLevelLauncher] Override set: {TestLevelJsonPath}. Opening Game scene...");

                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(GameScenePath);
                EditorApplication.isPlaying = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestLevelLauncher] Failed to launch: {ex}");
                SessionState.EraseString(OverrideSessionKey);
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

        [MenuItem("Tools/Test Level/Clear Test Override", priority = 53)]
        private static void ClearOverride()
        {
            SessionState.EraseString(OverrideSessionKey);
            Debug.Log("[TestLevelLauncher] Test level override cleared.");
        }
    }
}
#endif
