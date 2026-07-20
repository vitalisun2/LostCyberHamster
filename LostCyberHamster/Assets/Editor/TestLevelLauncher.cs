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
    /// Сервис запуска тестовых уровней через automation bridge.
    /// Сохраняет адрес уровня, открывает Bootstrap и включает Play Mode.
    /// Временные параметры запуска очищаются после выхода из Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class TestLevelLauncher
    {
        /// <summary>Ключ адреса тестового уровня, читаемый LoadMainMenuLoadingTask.</summary>
        public const string OverridePrefsKey = "TestLevel_Address";

        /// <summary>Ключ переопределения runtime timescale.</summary>
        public static string TimeScaleOverrideKey => Assets.Scripts.System.AutomationRuntimePrefs.TimeScaleOverrideKey;

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string LocationsRootPath = "Assets/Content/locations";
        private const string LevelsFolderName = "levels";
        private const string TestLevelPrefix = "test";

        /// <summary>Стандартный timescale для запуска через automation bridge.</summary>
        private const float AutomationDefaultTimeScale = 1.0f;

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
                Assets.Scripts.Tutorial.TutorialAutomation.Clear();
                Assets.Scripts.Tutorial.TutorialLaunchService.ClearCompletedResetRequest();
            }
        }

        /// <summary>
        /// Запускает тестовый уровень без UI-диалогов для automation bridge.
        /// </summary>
        public static bool TryLaunchTestLevelAutomation(string levelAddress, float timeScale, out string errorMessage)
        {
            float effectiveTimeScale = timeScale > 0f ? timeScale : AutomationDefaultTimeScale;
            return TryLaunchTestLevel(
                levelAddress,
                effectiveTimeScale,
                out errorMessage);
        }

        private static bool TryLaunchTestLevel(
            string levelAddress,
            float? timeScaleOverride,
            out string errorMessage)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                errorMessage = "Exit Play mode first.";
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
            PlayerPrefs.SetInt(Assets.Scripts.System.AutomationRuntimePrefs.SkipIntroKey, 1);

            bool isAutomatedTutorialRoute =
                Assets.Scripts.Tutorial.TutorialConstants.IsTutorialLevel(effectiveLevelAddress)
                || Assets.Scripts.Tutorial.TutorialConstants.IsFirstGameplayLevel(effectiveLevelAddress);

            if (isAutomatedTutorialRoute)
            {
                Assets.Scripts.Tutorial.TutorialAutomation.SetAutoPlay(true);
                Assets.Scripts.Tutorial.TutorialLaunchService.RequestCompletedResetOnce();
            }
            else
            {
                Assets.Scripts.Tutorial.TutorialAutomation.ClearAutoPlay();
                Assets.Scripts.Tutorial.TutorialLaunchService.ClearCompletedResetRequest();
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

        /// <summary>
        /// Собирает адреса тестовых уровней из Content/locations/*/levels.
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
    }
}
#endif
