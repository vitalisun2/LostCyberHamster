#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    public static class LostCyberHamsterBuildAutomation
    {
        private const string OutputArg = "-codexBuildOutput";
        private const string DevelopmentArg = "-codexBuildDevelopment";

        public static void BuildAndroidApk()
        {
            Build(BuildTarget.Android, BuildTargetGroup.Android);
        }

        public static void BuildWindows64()
        {
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone);
        }

        private static void Build(BuildTarget target, BuildTargetGroup targetGroup)
        {
            try
            {
                var outputRoot = GetOutputRoot();
                Directory.CreateDirectory(outputRoot);

                var development = GetBoolArg(DevelopmentArg);
                EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);
                BuildAddressables();

                var scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();

                if (scenes.Length == 0)
                    throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");

                var buildPath = GetBuildPath(outputRoot, target);
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath));

                var options = BuildOptions.None;
                if (development)
                    options |= BuildOptions.Development;

                var buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = target,
                    targetGroup = targetGroup,
                    locationPathName = buildPath,
                    options = options
                };

                var report = BuildPipeline.BuildPlayer(buildOptions);
                WriteSummary(outputRoot, buildPath, report, development);

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Build failed: {report.summary.result}");

                Debug.Log($"Build succeeded: {buildPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("Addressables settings were not found. Skipping Addressables build.");
                return;
            }

            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (!string.IsNullOrEmpty(result.Error))
                throw new InvalidOperationException($"Addressables build failed: {result.Error}");
        }

        private static string GetOutputRoot()
        {
            var output = GetArg(OutputArg);
            if (!string.IsNullOrEmpty(output))
                return Path.GetFullPath(output);

            return Path.GetFullPath(Path.Combine("..", "Builds", "telegram-buffer", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")));
        }

        private static string GetBuildPath(string outputRoot, BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return Path.Combine(outputRoot, "LostCyberHamster.apk");
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(outputRoot, "Windows", "LostCyberHamster.exe");
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported build target.");
            }
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return string.Empty;
        }

        private static bool GetBoolArg(string name)
        {
            return bool.TryParse(GetArg(name), out var value) && value;
        }

        private static void WriteSummary(string outputRoot, string buildPath, BuildReport report, bool development)
        {
            var summary = report.summary;
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"result\": \"{Escape(summary.result.ToString())}\",");
            builder.AppendLine($"  \"platform\": \"{Escape(summary.platform.ToString())}\",");
            builder.AppendLine($"  \"outputPath\": \"{Escape(buildPath)}\",");
            builder.AppendLine($"  \"totalSize\": {summary.totalSize},");
            builder.AppendLine($"  \"totalTime\": \"{Escape(summary.totalTime.ToString())}\",");
            builder.AppendLine($"  \"development\": {development.ToString().ToLowerInvariant()}");
            builder.AppendLine("}");

            File.WriteAllText(Path.Combine(outputRoot, $"build-summary-unity-{summary.platform}.json"), builder.ToString());
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
#endif
