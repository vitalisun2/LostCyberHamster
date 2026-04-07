#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string LocationsRootPath = "Assets/Content/locations";
        private const string LevelsFolderName = "levels";
        private const string TestLevelPrefix = "test";

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

        /// <summary>
        /// Открывает вычисляемый список test-level адресов из Content/locations.
        /// Unity не поддерживает динамическое создание MenuItem из данных на диске,
        /// поэтому список строится во всплывающем GenericMenu по статическому пункту меню.
        /// </summary>
        [MenuItem("Tools/Test Level/Launch...", priority = 50)]
        private static void ShowLaunchMenu()
        {
            var testLevels = DiscoverTestLevels();
            if (testLevels.Count == 0)
            {
                EditorUtility.DisplayDialog("Test Level", $"No test levels found under '{LocationsRootPath}'.", "OK");
                return;
            }

            // Build a fresh list every time so menu contents reflect current files on disk.
            var menu = new GenericMenu();
            foreach (var testLevel in testLevels)
            {
                var capturedLevel = testLevel;
                menu.AddItem(new GUIContent(capturedLevel.MenuLabel), false, () => LaunchInteractive(capturedLevel.Address));
            }

            menu.ShowAsContext();
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

            var effectiveLevelAddress = levelAddress?.Trim();
            if (string.IsNullOrWhiteSpace(effectiveLevelAddress) && !TryGetDefaultTestLevelAddress(out effectiveLevelAddress))
            {
                errorMessage = $"No test levels found under '{LocationsRootPath}'.";
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

        /// <summary>
        /// Собирает список test-level адресов из Content/locations/*/levels.
        /// </summary>
        private static List<TestLevelEntry> DiscoverTestLevels()
        {
            var testLevels = new List<TestLevelEntry>();
            if (!TryGetLocationsRootAbsolutePath(out var locationsRootAbsolutePath) ||
                !Directory.Exists(locationsRootAbsolutePath))
            {
                return testLevels;
            }

            // Scan each location independently so addresses keep the location/daypart prefix.
            foreach (var locationDirectory in Directory.EnumerateDirectories(locationsRootAbsolutePath))
            {
                var locationName = Path.GetFileName(locationDirectory);
                if (string.IsNullOrWhiteSpace(locationName))
                {
                    continue;
                }

                var levelsDirectory = Path.Combine(locationDirectory, LevelsFolderName);
                if (!Directory.Exists(levelsDirectory))
                {
                    continue;
                }

                foreach (var descriptor in LevelDataManager.GetLevelFileDescriptors(levelsDirectory))
                {
                    if (TryBuildTestLevelEntry(locationName, descriptor, out var testLevel))
                    {
                        testLevels.Add(testLevel);
                    }
                }
            }

            testLevels.Sort((left, right) => string.CompareOrdinal(left.MenuLabel, right.MenuLabel));
            return testLevels;
        }

        private static void LaunchInteractive(string levelAddress)
        {
            if (!TryLaunchTestLevel(interactive: true, levelAddress, ToolsDefaultTimeScale, out var errorMessage))
            {
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
            }
        }

        private static bool TryGetDefaultTestLevelAddress(out string levelAddress)
        {
            levelAddress = DiscoverTestLevels()
                .Select(level => level.Address)
                .FirstOrDefault();
            return !string.IsNullOrWhiteSpace(levelAddress);
        }

        private static bool TryBuildTestLevelEntry(string locationName, LevelFileDescriptor descriptor, out TestLevelEntry testLevel)
        {
            var levelKey = ExtractLevelKey(descriptor.RelativePath);
            if (!levelKey.StartsWith(TestLevelPrefix, StringComparison.OrdinalIgnoreCase))
            {
                testLevel = default;
                return false;
            }

            var normalizedRelativePath = NormalizePath(descriptor.RelativePath);
            var relativeDirectory = NormalizePath(Path.GetDirectoryName(normalizedRelativePath));
            var address = string.IsNullOrWhiteSpace(relativeDirectory)
                ? $"{locationName}/{levelKey}"
                : $"{locationName}/{relativeDirectory}";

            testLevel = new TestLevelEntry(address, address);
            return true;
        }

        private static string ExtractLevelKey(string relativePath)
        {
            var normalizedPath = NormalizePath(relativePath);
            var directoryName = NormalizePath(Path.GetDirectoryName(normalizedPath));
            if (!string.IsNullOrWhiteSpace(directoryName))
            {
                var segments = directoryName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    return segments[^1];
                }
            }

            return Path.GetFileNameWithoutExtension(normalizedPath);
        }

        private static bool TryGetLocationsRootAbsolutePath(out string absolutePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            absolutePath = Path.GetFullPath(Path.Combine(projectRoot, LocationsRootPath));
            return !string.IsNullOrWhiteSpace(absolutePath);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/');
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

        private readonly struct TestLevelEntry
        {
            public TestLevelEntry(string address, string menuLabel)
            {
                Address = address;
                MenuLabel = menuLabel;
            }

            public string Address { get; }

            public string MenuLabel { get; }
        }
    }
}
#endif
