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

        /// <summary>PlayerPrefs key: auto-enable Bot when test level loads.</summary>
        public const string BotAutoStartKey = "BotAutoStart";

        /// <summary>PlayerPrefs key: runtime timescale override. Shared with <see cref="Assets.Scripts.System.AutomationRuntimePrefs"/>.</summary>
        public static string TimeScaleOverrideKey => Assets.Scripts.System.AutomationRuntimePrefs.TimeScaleOverrideKey;

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string SwitchLaneTestLevelAddress = "01_New_York/Morning/test_threat_small_notalive_road_switchlane";
        private const string JumpTestLevelAddress = "01_New_York/Morning/test_threat_small_notalive_road_jump";
        private const string BigAliveTestLevelAddress = "01_New_York/Morning/test_threat_bigalive";
        private const string JumpOnRoofTestLevelAddress = "01_New_York/Morning/test_jump_on_roof";

        /// <summary>Default timescale when launching via Tools menu for interactive visual inspection.</summary>
        private const float ToolsDefaultTimeScale = 1.0f;

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

        [MenuItem("Tools/Test Level/Launch test_threat_small_notalive_road_switchlane", priority = 50)]
        private static void LaunchSwitchLane()
        {
            if (!TryLaunchTestLevel(interactive: true, SwitchLaneTestLevelAddress, ToolsDefaultTimeScale, out var errorMessage))
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
        }

        [MenuItem("Tools/Test Level/Launch test_threat_small_notalive_road_jump", priority = 51)]
        private static void LaunchJump()
        {
            if (!TryLaunchTestLevel(interactive: true, JumpTestLevelAddress, ToolsDefaultTimeScale, out var errorMessage))
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
        }

        [MenuItem("Tools/Test Level/Launch test_threat_bigalive", priority = 52)]
        private static void LaunchBigAlive()
        {
            if (!TryLaunchTestLevel(interactive: true, BigAliveTestLevelAddress, ToolsDefaultTimeScale, out var errorMessage))
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
        }

        [MenuItem("Tools/Test Level/Launch test_jump_on_roof", priority = 53)]
        private static void LaunchJumpOnRoof()
        {
            if (!TryLaunchTestLevel(interactive: true, JumpOnRoofTestLevelAddress, ToolsDefaultTimeScale, out var errorMessage))
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
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
                ? SwitchLaneTestLevelAddress
                : levelAddress.Trim();

            // Write override into PlayerPrefs so it survives domain reload on Play
            PlayerPrefs.SetString(OverridePrefsKey, effectiveLevelAddress);
            PlayerPrefs.SetInt(BotAutoStartKey, 1);
            PlayerPrefs.SetInt(Assets.Scripts.System.AutomationRuntimePrefs.SkipIntroKey, interactive ? 0 : 1);

            if (timeScaleOverride.HasValue)
                PlayerPrefs.SetFloat(TimeScaleOverrideKey, Mathf.Clamp(timeScaleOverride.Value, 0.1f, 4.0f));
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
