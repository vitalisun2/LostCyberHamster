#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Editor tool for launching test levels directly from the menu.
    /// Writes the target level address into PlayerPrefs, opens the Bootstrap
    /// scene and enters Play mode. LoadMainMenuLoadingTask detects the
    /// override, sets CurrentLevel, and loads Game instead of Menu.
    /// The full bootstrap pipeline (auth, catalog, addressables) runs normally.
    /// </summary>
    public static class TestLevelLauncher
    {
        /// <summary>PlayerPrefs key checked by LoadMainMenuLoadingTask.</summary>
        public const string OverridePrefsKey = "TestLevel_Address";

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string TestLevelAddress = "01_New_York/Test/test_medium_notalive";
        private const string TestLevelJsonPath =
            "Assets/Content/locations/01_New_York/levels/Test/test_medium_notalive/test_medium_notalive.json";

        [MenuItem("Tools/Test Level/Launch Medium NotAlive Test", priority = 50)]
        private static void LaunchTestLevel()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Test Level",
                    "Exit Play mode first.", "OK");
                return;
            }

            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(TestLevelJsonPath);
            if (json == null)
            {
                EditorUtility.DisplayDialog("Test Level",
                    $"Test level JSON not found:\n{TestLevelJsonPath}", "OK");
                return;
            }

            // Write override into PlayerPrefs so it survives domain reload on Play
            PlayerPrefs.SetString(OverridePrefsKey, TestLevelAddress);
            PlayerPrefs.Save();

            Debug.Log($"[TestLevelLauncher] Override set: {TestLevelAddress}. Starting Bootstrap...");

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
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
            PlayerPrefs.DeleteKey(OverridePrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[TestLevelLauncher] Test level override cleared.");
        }
    }
}
#endif
