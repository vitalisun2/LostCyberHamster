#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary>PlayerPrefs key: make BotV2 primary in runtime bootstrap.</summary>
        public const string BotV2PrimaryEnabledKey = "BotV2PrimaryEnabled";

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
                if (PlayerPrefs.HasKey(BotV2PrimaryEnabledKey))
                {
                    PlayerPrefs.DeleteKey(BotV2PrimaryEnabledKey);
                    PlayerPrefs.Save();
                }
            }
        }

        [MenuItem("Tools/Test Level/Launch", priority = 50)]
        private static void LaunchTestLevel()
        {
            if (!TryLaunchTestLevel(interactive: true, out var errorMessage))
            {
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
            }
        }

        /// <summary>
        /// Запускает test level без UI-диалогов, чтобы этим можно было безопасно управлять из automation bridge.
        /// </summary>
        public static bool TryLaunchTestLevelAutomation(out string errorMessage)
        {
            return TryLaunchTestLevel(interactive: false, out errorMessage);
        }

        private static bool TryLaunchTestLevel(bool interactive, out string errorMessage)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                errorMessage = "Exit Play mode first.";
                return false;
            }

            if (!interactive && HasDirtyOpenScenes())
            {
                errorMessage = "Automation launch requires all open scenes to be saved first.";
                return false;
            }

            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                errorMessage = "Launch cancelled because modified scenes were not saved.";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) == null)
            {
                errorMessage = $"Bootstrap scene not found: {BootstrapScenePath}";
                return false;
            }

            // Write override into PlayerPrefs so it survives domain reload on Play
            PlayerPrefs.SetString(OverridePrefsKey, TestLevelAddress);
            PlayerPrefs.SetInt(BotAutoStartKey, 1);
            PlayerPrefs.SetInt(BotV2PrimaryEnabledKey, 1);
            PlayerPrefs.Save();

            Debug.Log($"[TestLevelLauncher] Override set: {TestLevelAddress}. Starting Bootstrap...");
            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
            errorMessage = null;
            return true;
        }

        private static bool HasDirtyOpenScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
