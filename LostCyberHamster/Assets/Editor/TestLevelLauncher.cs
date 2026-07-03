#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>PlayerPrefs key: runtime timescale override. Shared with <see cref="Assets.Scripts.System.AutomationRuntimePrefs"/>.</summary>
        public static string TimeScaleOverrideKey => Assets.Scripts.System.AutomationRuntimePrefs.TimeScaleOverrideKey;

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string LocationsRootPath = "Assets/Content/locations";
        private const string LevelsFolderName = "levels";
        private const string TestLevelPrefix = "test";
        private const string PendingInteractiveLaunchAddressSessionKey = "TestLevelLauncher.PendingInteractiveLaunchAddress";
        private const string LaunchedInteractiveAddressesSessionKey = "TestLevelLauncher.LaunchedInteractiveAddresses";
        private const string LastLaunchedInteractiveAddressSessionKey = "TestLevelLauncher.LastLaunchedInteractiveAddress";

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
                if (PlayerPrefs.HasKey(Assets.Scripts.Tutorial.TutorialAutomationSettings.AutoPlayKey))
                {
                    PlayerPrefs.DeleteKey(Assets.Scripts.Tutorial.TutorialAutomationSettings.AutoPlayKey);
                    PlayerPrefs.Save();
                }
                if (PlayerPrefs.HasKey(Assets.Scripts.Tutorial.TutorialLaunchState.ResetCompletedOnceKey))
                {
                    PlayerPrefs.DeleteKey(Assets.Scripts.Tutorial.TutorialLaunchState.ResetCompletedOnceKey);
                    PlayerPrefs.Save();
                }
                if (PlayerPrefs.HasKey(Assets.Scripts.Tutorial.TutorialAutomationSettings.StopAfterStepKey))
                {
                    PlayerPrefs.DeleteKey(Assets.Scripts.Tutorial.TutorialAutomationSettings.StopAfterStepKey);
                    PlayerPrefs.Save();
                }

                LaunchPendingInteractiveLevelWhenReady();
            }
        }

        /// <summary>
        /// Opens the test-level address picker from Content/locations.
        /// Unity does not support dynamic MenuItem creation from disk data,
        /// so the list is built in a helper window from a static menu item.
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
        /// Launches a test level without UI dialogs for automation bridge control.
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

            bool isAutomatedTutorialRoute =
                !interactive
                && (Assets.Scripts.Tutorial.TutorialConstants.IsTutorialLevel(effectiveLevelAddress)
                    || Assets.Scripts.Tutorial.TutorialConstants.IsFirstGameplayLevel(effectiveLevelAddress));

            if (isAutomatedTutorialRoute)
            {
                PlayerPrefs.SetInt(Assets.Scripts.Tutorial.TutorialAutomationSettings.AutoPlayKey, 1);
                PlayerPrefs.SetInt(Assets.Scripts.Tutorial.TutorialLaunchState.ResetCompletedOnceKey, 1);
            }
            else
            {
                if (PlayerPrefs.HasKey(Assets.Scripts.Tutorial.TutorialAutomationSettings.AutoPlayKey))
                {
                    PlayerPrefs.DeleteKey(Assets.Scripts.Tutorial.TutorialAutomationSettings.AutoPlayKey);
                }

                if (PlayerPrefs.HasKey(Assets.Scripts.Tutorial.TutorialLaunchState.ResetCompletedOnceKey))
                {
                    PlayerPrefs.DeleteKey(Assets.Scripts.Tutorial.TutorialLaunchState.ResetCompletedOnceKey);
                }
            }

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
            EditorApplication.delayCall += () =>
            {
                LaunchInteractive(levelAddress);
            };
        }

        /// <summary>
        /// Collects test-level addresses from Content/locations/*/levels.
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

        private static bool LaunchInteractive(string levelAddress)
        {
            if (!TryLaunchTestLevel(interactive: true, levelAddress, ToolsDefaultTimeScale, out var errorMessage))
            {
                EditorUtility.DisplayDialog("Test Level", errorMessage, "OK");
                return false;
            }

            return true;
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
            private const float MinWindowWidth = 360f;
            private const float MinWindowHeight = 360f;
            private const float LaunchWithoutStartToggleWidth = 160f;
            private static readonly Color LaunchedLevelBackgroundColor = new(0.68f, 0.86f, 0.68f, 1f);
            private static readonly Color LaunchedBadgeTextColor = new(0.12f, 0.42f, 0.16f, 1f);
            private static readonly Color LastLaunchedBadgeTextColor = new(0.1f, 0.3f, 0.72f, 1f);

            private readonly List<TestLevelEntry> _testLevels = new();
            private readonly HashSet<string> _launchedLevelAddresses = new(StringComparer.Ordinal);
            private string _lastLaunchedLevelAddress;
            private bool _launchWithoutStart;
            private Vector2 _scrollPosition;
            private static GUIStyle _launchedBadgeStyle;
            private static GUIStyle _lastLaunchedBadgeStyle;

            public static void ShowWindow(IEnumerable<TestLevelEntry> testLevels)
            {
                var entries = testLevels?.ToList() ?? new List<TestLevelEntry>();
                if (entries.Count == 0)
                {
                    EditorUtility.DisplayDialog("Test Level", $"No test levels found under '{LocationsRootPath}'.", "OK");
                    return;
                }

                ClearLaunchedLevelAddressesStorage();

                var window = GetWindow<TestLevelPickerWindow>("Test Levels");
                window.titleContent = new GUIContent("Test Levels");
                window._testLevels.Clear();
                window._testLevels.AddRange(entries);
                window._launchWithoutStart = false;
                window.LoadLaunchedLevelAddresses();
                window.ApplyWindowSize();
                window.Focus();
            }

            private void OnEnable()
            {
                LoadLaunchedLevelAddresses();

                if (_testLevels.Count == 0)
                {
                    RefreshLevels(resetLaunchedState: false);
                }
            }

            private void OnGUI()
            {
                // Header and window commands.
                EditorGUILayout.HelpBox("Choose a test level to launch through Bootstrap with bot auto-start.", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Found: {_testLevels.Count}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    _launchWithoutStart = GUILayout.Toggle(
                        _launchWithoutStart,
                        "Launch without start",
                        GUILayout.Width(LaunchWithoutStartToggleWidth));

                    if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                    {
                        RefreshLevels(resetLaunchedState: true);
                    }
                }

                GUILayout.Space(6f);

                // Empty state.
                if (_testLevels.Count == 0)
                {
                    EditorGUILayout.HelpBox($"No test levels found under '{LocationsRootPath}'.", MessageType.Warning);
                    return;
                }

                // Test level list.
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                foreach (var testLevel in _testLevels)
                {
                    DrawTestLevelRow(testLevel);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawTestLevelRow(TestLevelEntry testLevel)
            {
                // Display state.
                var wasLaunched = _launchedLevelAddresses.Contains(testLevel.Address);
                var isLastLaunched = wasLaunched &&
                    string.Equals(testLevel.Address, _lastLaunchedLevelAddress, StringComparison.Ordinal);

                // Row surface.
                var previousBackgroundColor = GUI.backgroundColor;
                if (wasLaunched)
                {
                    GUI.backgroundColor = LaunchedLevelBackgroundColor;
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    GUI.backgroundColor = previousBackgroundColor;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(testLevel.MenuLabel, EditorStyles.boldLabel);

                        if (wasLaunched)
                        {
                            GUILayout.Label("Launched", GetLaunchedBadgeStyle(isLastLaunched), GUILayout.Width(72f));
                        }

                        // Launch command.
                        if (GUILayout.Button("Launch", GUILayout.Width(90f)))
                        {
                            LaunchOrMarkTestLevel(testLevel.Address);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            private void LaunchOrMarkTestLevel(string levelAddress)
            {
                if (_launchWithoutStart)
                {
                    RegisterLaunchedLevel(levelAddress);
                    return;
                }

                if (LaunchInteractive(levelAddress))
                {
                    RegisterLaunchedLevel(levelAddress);
                }
            }

            private static GUIStyle GetLaunchedBadgeStyle(bool isLastLaunched)
            {
                if (isLastLaunched)
                {
                    if (_lastLaunchedBadgeStyle == null)
                    {
                        _lastLaunchedBadgeStyle = CreateLaunchedBadgeStyle(LastLaunchedBadgeTextColor);
                    }

                    return _lastLaunchedBadgeStyle;
                }

                if (_launchedBadgeStyle == null)
                {
                    _launchedBadgeStyle = CreateLaunchedBadgeStyle(LaunchedBadgeTextColor);
                }

                return _launchedBadgeStyle;
            }

            private static GUIStyle CreateLaunchedBadgeStyle(Color textColor)
            {
                var style = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
                style.normal.textColor = textColor;
                return style;
            }

            private void RegisterLaunchedLevel(string levelAddress)
            {
                _launchedLevelAddresses.Add(levelAddress);
                _lastLaunchedLevelAddress = levelAddress;
                SaveLaunchedLevelAddresses();
            }

            private void RefreshLevels(bool resetLaunchedState)
            {
                _testLevels.Clear();
                if (resetLaunchedState)
                {
                    ClearLaunchedLevelAddresses();
                }

                _testLevels.AddRange(DiscoverTestLevels());
                ApplyWindowSize();
            }

            private void LoadLaunchedLevelAddresses()
            {
                _launchedLevelAddresses.Clear();
                _lastLaunchedLevelAddress = SessionState.GetString(LastLaunchedInteractiveAddressSessionKey, string.Empty);

                var launchedAddresses = SessionState.GetString(LaunchedInteractiveAddressesSessionKey, string.Empty);
                if (string.IsNullOrWhiteSpace(launchedAddresses))
                {
                    return;
                }

                foreach (var launchedAddress in launchedAddresses.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    _launchedLevelAddresses.Add(launchedAddress);
                }
            }

            private void SaveLaunchedLevelAddresses()
            {
                SessionState.SetString(
                    LaunchedInteractiveAddressesSessionKey,
                    string.Join("\n", _launchedLevelAddresses.OrderBy(address => address, StringComparer.Ordinal)));
                SessionState.SetString(LastLaunchedInteractiveAddressSessionKey, _lastLaunchedLevelAddress ?? string.Empty);
            }

            private void ClearLaunchedLevelAddresses()
            {
                _launchedLevelAddresses.Clear();
                _lastLaunchedLevelAddress = string.Empty;
                ClearLaunchedLevelAddressesStorage();
            }

            private static void ClearLaunchedLevelAddressesStorage()
            {
                SessionState.SetString(LaunchedInteractiveAddressesSessionKey, string.Empty);
                SessionState.SetString(LastLaunchedInteractiveAddressSessionKey, string.Empty);
            }

            private void ApplyWindowSize()
            {
                minSize = new Vector2(MinWindowWidth, MinWindowHeight);
                maxSize = new Vector2(4096f, 4096f);
            }
        }
    }
}
#endif
