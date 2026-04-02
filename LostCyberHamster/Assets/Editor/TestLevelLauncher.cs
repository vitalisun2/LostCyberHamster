#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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
    ///
    /// Test levels are discovered automatically by scanning
    /// Assets/Content/locations for folders named test_*.
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
        private const string LocationsRoot = "Assets/Content/locations";

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

        [MenuItem("Tools/Test Level/Select...", priority = 50)]
        private static void ShowTestLevelMenu()
        {
            var levels = DiscoverTestLevels();
            if (levels.Count == 0)
            {
                EditorUtility.DisplayDialog("Test Level", "No test levels found in " + LocationsRoot, "OK");
                return;
            }

            var menu = new GenericMenu();
            foreach (var address in levels)
            {
                string captured = address;
                menu.AddItem(new GUIContent(captured), false, () =>
                {
                    if (!TryLaunchTestLevel(interactive: true, captured, ToolsDefaultTimeScale, out var errorMessage))
                        EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Сканирует Assets/Content/locations, находит все папки test_* с JSON-файлом уровня.
        /// Возвращает отсортированный список адресов вида "01_New_York/Morning/test_jump_on_roof".
        /// </summary>
        private static List<string> DiscoverTestLevels()
        {
            var result = new List<string>();
            string fullRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Content/locations"));

            if (!Directory.Exists(fullRoot))
                return result;

            // locations/<location>/levels/<daypart>/test_*
            foreach (string locationDir in Directory.GetDirectories(fullRoot))
            {
                string location = Path.GetFileName(locationDir);
                string levelsDir = Path.Combine(locationDir, "levels");
                if (!Directory.Exists(levelsDir))
                    continue;

                foreach (string daypartDir in Directory.GetDirectories(levelsDir))
                {
                    string daypart = Path.GetFileName(daypartDir);

                    foreach (string testDir in Directory.GetDirectories(daypartDir))
                    {
                        string folderName = Path.GetFileName(testDir);
                        if (!folderName.StartsWith("test_"))
                            continue;

                        string jsonPath = Path.Combine(testDir, folderName + ".json");
                        if (!File.Exists(jsonPath))
                            continue;

                        result.Add($"{location}/{daypart}/{folderName}");
                    }
                }
            }

            result.Sort();
            return result;
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
                ? string.Empty
                : levelAddress.Trim();

            if (string.IsNullOrEmpty(effectiveLevelAddress))
            {
                errorMessage = "Level address is empty.";
                return false;
            }

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
