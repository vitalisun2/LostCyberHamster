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
    /// Override auto-clears when Play Mode exits.
    /// </summary>
    [InitializeOnLoad]
    public static class TestLevelLauncher
    {
        /// <summary>PlayerPrefs key checked by LoadMainMenuLoadingTask.</summary>
        public const string OverridePrefsKey = "TestLevel_Address";

        /// <summary>PlayerPrefs key: auto-enable HamsterBot when test level loads.</summary>
        public const string BotAutoStartKey = "BotAutoStart";

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string TestLevelAddress = "01_New_York/Morning/test_level";

        static TestLevelLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Auto-clear override when exiting Play Mode
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (PlayerPrefs.HasKey(OverridePrefsKey))
                {
                    PlayerPrefs.DeleteKey(OverridePrefsKey);
                    PlayerPrefs.Save();
                }
                if (PlayerPrefs.HasKey(BotAutoStartKey))
                {
                    PlayerPrefs.DeleteKey(BotAutoStartKey);
                    PlayerPrefs.Save();
                }
            }
        }

        [MenuItem("Tools/Test Level/Launch", priority = 50)]
        private static void LaunchTestLevel()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Test Level",
                    "Exit Play mode first.", "OK");
                return;
            }

            // Write override into PlayerPrefs so it survives domain reload on Play
            PlayerPrefs.SetString(OverridePrefsKey, TestLevelAddress);
            PlayerPrefs.SetInt(BotAutoStartKey, 1);
            PlayerPrefs.Save();

            Debug.Log($"[TestLevelLauncher] Override set: {TestLevelAddress}. Starting Bootstrap...");

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
