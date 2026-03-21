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

        /// <summary>PlayerPrefs key: runtime timescale override for automated test runs.</summary>
        public const string TimeScaleOverrideKey = "TestLevel_TimeScale";

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
                if (PlayerPrefs.HasKey(TimeScaleOverrideKey))
                {
                    PlayerPrefs.DeleteKey(TimeScaleOverrideKey);
                    PlayerPrefs.Save();
                }
                if (PlayerPrefs.HasKey(Assets.Scripts.System.AutomationRuntimePrefs.SkipIntroKey))
                {
                    PlayerPrefs.DeleteKey(Assets.Scripts.System.AutomationRuntimePrefs.SkipIntroKey);
                    PlayerPrefs.Save();
                }
            }
        }

        [MenuItem("Tools/Test Level/Launch", priority = 50)]
        private static void LaunchTestLevel()
        {
            if (!TryLaunchTestLevel(interactive: true, TestLevelAddress, null, out var errorMessage))
            {
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
            }
        }

        /// <summary>
        /// Запускает test level без UI-диалогов, чтобы этим можно было безопасно управлять из automation bridge.
        /// </summary>
        public static bool TryLaunchTestLevelAutomation(string levelAddress, float timeScale, out string errorMessage)
        {
            float? timeScaleOverride = timeScale > 0f ? timeScale : null;
            return TryLaunchTestLevel(
                interactive: false,
                levelAddress,
                timeScaleOverride,
                out errorMessage);
        }

        private static bool TryLaunchTestLevel(
            bool interactive,
            string levelAddress,
            float? timeScaleOverride,
            out string errorMessage)
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

            string effectiveLevelAddress = string.IsNullOrWhiteSpace(levelAddress)
                ? TestLevelAddress
                : levelAddress.Trim();

            // Write override into PlayerPrefs so it survives domain reload on Play
            PlayerPrefs.SetString(OverridePrefsKey, effectiveLevelAddress);
            PlayerPrefs.SetInt(BotAutoStartKey, 1);
            PlayerPrefs.SetInt(BotV2PrimaryEnabledKey, 1);
            PlayerPrefs.SetInt(Assets.Scripts.System.AutomationRuntimePrefs.SkipIntroKey, interactive ? 0 : 1);

            if (timeScaleOverride.HasValue)
                PlayerPrefs.SetFloat(TimeScaleOverrideKey, Mathf.Clamp(timeScaleOverride.Value, 0.1f, 2.0f));
            else if (PlayerPrefs.HasKey(TimeScaleOverrideKey))
                PlayerPrefs.DeleteKey(TimeScaleOverrideKey);

            PlayerPrefs.Save();

            string timeScalePart = timeScaleOverride.HasValue
                ? $", timeScale={timeScaleOverride.Value:F2}"
                : string.Empty;
            Debug.Log($"[TestLevelLauncher] Override set: {effectiveLevelAddress}{timeScalePart}. Starting Bootstrap...");
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
