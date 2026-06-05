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

        /// <summary>PlayerPrefs key: runtime timescale override. Shared with <see cref="Assets.Scripts.System.AutomationRuntimePrefs"/>.</summary>
        public static string TimeScaleOverrideKey => Assets.Scripts.System.AutomationRuntimePrefs.TimeScaleOverrideKey;

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string LocationsRootPath = "Assets/Content/locations";
        private const string LevelsFolderName = "levels";
        private const string TestLevelPrefix = "test";
        private const string PendingInteractiveLaunchAddressSessionKey = "TestLevelLauncher.PendingInteractiveLaunchAddress";

        /// <summary>Default timescale when launching through the automation bridge.</summary>
        private const float AutomationDefaultTimeScale = 1.0f;

        /// <summary>Default timescale when launching via Tools menu for interactive visual inspection.</summary>
        private const float ToolsDefaultTimeScale = 1.0f;

        static TestLevelLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Auto-clear override when exiting Play Mode.
                if (PlayerPrefs.HasKey(OverridePrefsKey))
                {
                    PlayerPrefs.DeleteKey(OverridePrefsKey);
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

                LaunchPendingInteractiveLevelWhenReady();
            }
        }

        /// <summary>
        /// Открывает окно выбора test-level адреса из Content/locations.
        /// Unity не поддерживает динамическое создание MenuItem из данных на диске,
        /// поэтому список строится во вспомогательном окне по статическому пункту меню.
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

            TestLevelPickerWindow.ShowWindow(testLevels);
        }

        /// <summary>
        /// Запускает test level без UI-диалогов, чтобы этим можно было безопасно управлять из automation bridge.
        /// </summary>
        public static bool TryLaunchTestLevelAutomation(string levelAddress, float timeScale, out string errorMessage)
        {
            float effectiveTimeScale = timeScale > 0f ? timeScale : AutomationDefaultTimeScale;
            return TryLaunchTestLevel(
                interactive: false,
                levelAddress,
                effectiveTimeScale,
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
                if (interactive)
                {
                    RequestInteractiveRelaunch(levelAddress);
                    errorMessage = null;
                    return true;
                }

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
            PlayerPrefs.SetInt(Assets.Scripts.System.AutomationRuntimePrefs.SkipIntroKey, interactive ? 0 : 1);

            if (timeScaleOverride.HasValue)
                PlayerPrefs.SetFloat(TimeScaleOverrideKey, Mathf.Clamp(timeScaleOverride.Value, 0.1f, 4.0f));
            else if (PlayerPrefs.HasKey(TimeScaleOverrideKey))
                PlayerPrefs.DeleteKey(TimeScaleOverrideKey);

            PlayerPrefs.Save();

            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
            errorMessage = null;
            return true;
        }

        private static void RequestInteractiveRelaunch(string levelAddress)
        {
            SessionState.SetString(PendingInteractiveLaunchAddressSessionKey, levelAddress ?? string.Empty);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += LaunchPendingInteractiveLevelWhenReady;
        }

        private static void LaunchPendingInteractiveLevelWhenReady()
        {
            var levelAddress = SessionState.GetString(PendingInteractiveLaunchAddressSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(levelAddress))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += LaunchPendingInteractiveLevelWhenReady;
                return;
            }

            SessionState.SetString(PendingInteractiveLaunchAddressSessionKey, string.Empty);
            EditorApplication.delayCall += () => LaunchInteractive(levelAddress);
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

        private sealed class TestLevelPickerWindow : EditorWindow
        {
            private const float WindowWidth = 640f;
            private const float MinWindowHeight = 360f;
            private const float ScreenVerticalMargin = 96f;
            private const float HeaderHeight = 96f;
            private const float LevelRowHeight = 32f;

            private readonly List<TestLevelEntry> _testLevels = new();
            private Vector2 _scrollPosition;

            public static void ShowWindow(IEnumerable<TestLevelEntry> testLevels)
            {
                var entries = testLevels?.ToList() ?? new List<TestLevelEntry>();
                if (entries.Count == 0)
                {
                    EditorUtility.DisplayDialog("Test Level", $"No test levels found under '{LocationsRootPath}'.", "OK");
                    return;
                }

                var window = CreateInstance<TestLevelPickerWindow>();
                window.titleContent = new GUIContent("Test Levels");
                window._testLevels.Clear();
                window._testLevels.AddRange(entries);
                window.ApplyWindowSize();
                window.position = new Rect(
                    (Screen.currentResolution.width - WindowWidth) * 0.5f,
                    (Screen.currentResolution.height - window.minSize.y) * 0.5f,
                    WindowWidth,
                    window.minSize.y);
                window.ShowUtility();
                window.Focus();
            }

            private void OnEnable()
            {
                if (_testLevels.Count == 0)
                {
                    RefreshLevels();
                }
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("Test Levels", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Выбери test level для запуска через Bootstrap с автовключением бота.", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Found: {_testLevels.Count}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                    {
                        RefreshLevels();
                    }

                    if (GUILayout.Button("Close", GUILayout.Width(80f)))
                    {
                        Close();
                    }
                }

                GUILayout.Space(6f);

                if (_testLevels.Count == 0)
                {
                    EditorGUILayout.HelpBox($"No test levels found under '{LocationsRootPath}'.", MessageType.Warning);
                    return;
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                foreach (var testLevel in _testLevels)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(testLevel.MenuLabel, EditorStyles.boldLabel);

                            if (GUILayout.Button("Launch", GUILayout.Width(90f)))
                            {
                                LaunchInteractive(testLevel.Address);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            private void RefreshLevels()
            {
                _testLevels.Clear();
                _testLevels.AddRange(DiscoverTestLevels());
                ApplyWindowSize();
            }

            private void ApplyWindowSize()
            {
                var screenHeight = Screen.currentResolution.height > 0
                    ? Screen.currentResolution.height
                    : MinWindowHeight;
                var maxWindowHeight = Mathf.Max(MinWindowHeight, screenHeight - ScreenVerticalMargin);
                var contentHeight = HeaderHeight + _testLevels.Count * LevelRowHeight;
                var windowHeight = Mathf.Clamp(contentHeight, MinWindowHeight, maxWindowHeight);

                minSize = new Vector2(WindowWidth, windowHeight);
                maxSize = new Vector2(WindowWidth, windowHeight);

                if (position.width > 0f)
                {
                    position = new Rect(position.x, position.y, WindowWidth, windowHeight);
                }
            }
        }
    }
}
#endif
