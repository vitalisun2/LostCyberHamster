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
        private const string ShowDevelopmentConsoleArg = "-codexShowDevelopmentConsole";
        private const string ShowDevelopmentConsoleDefine = "LCH_SHOW_DEVELOPMENT_CONSOLE";
        private const string AndroidSigningConfigArg = "-lostCyberHamsterAndroidSigningConfig";
        private const string AndroidSigningConfigEnvironmentVariable = "LOSTCYBERHAMSTER_ANDROID_SIGNING_CONFIG";
        private const string DefaultAndroidSigningConfigRelativePath = @".lostcyberhamster\android-dev-signing\signing.local.json";

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
                var showDevelopmentConsole = GetBoolArg(ShowDevelopmentConsoleArg);
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
                    options = options,
                    extraScriptingDefines = showDevelopmentConsole
                        ? new[] { ShowDevelopmentConsoleDefine }
                        : Array.Empty<string>()
                };

                AndroidSigningScope androidSigning = null;
                try
                {
                    androidSigning = ApplyAndroidSigningIfNeeded(target);

                    var report = BuildPipeline.BuildPlayer(buildOptions);
                    WriteSummary(
                        outputRoot,
                        buildPath,
                        report,
                        development,
                        showDevelopmentConsole,
                        androidSigning);

                    if (report.summary.result != BuildResult.Succeeded)
                        throw new InvalidOperationException($"Build failed: {report.summary.result}");
                }
                finally
                {
                    androidSigning?.Dispose();
                }

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

        private static AndroidSigningScope ApplyAndroidSigningIfNeeded(BuildTarget target)
        {
            if (target != BuildTarget.Android)
                return null;

            var configPath = GetAndroidSigningConfigPath();
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    "Android dev signing config was not found. " +
                    "Create it with tools/build/install_android_dev_signing.ps1 or pass " +
                    $"{AndroidSigningConfigArg} <path>.",
                    configPath);
            }

            var config = JsonUtility.FromJson<AndroidSigningConfig>(File.ReadAllText(configPath));
            if (config == null)
                throw new InvalidOperationException($"Android signing config is invalid JSON: {configPath}");

            RequireConfigValue(config.keystorePath, nameof(config.keystorePath), configPath);
            RequireConfigValue(config.keystorePass, nameof(config.keystorePass), configPath);
            RequireConfigValue(config.keyaliasName, nameof(config.keyaliasName), configPath);
            RequireConfigValue(config.keyaliasPass, nameof(config.keyaliasPass), configPath);

            var keystorePath = ResolveConfiguredPath(config.keystorePath, Path.GetDirectoryName(configPath));
            if (!File.Exists(keystorePath))
                throw new FileNotFoundException("Android signing keystore was not found.", keystorePath);

            var signingScope = AndroidSigningScope.Apply(config, configPath, keystorePath);
            Debug.Log(
                "Android dev signing configured. " +
                $"alias={signingScope.KeyAliasName} certSha256={signingScope.CertificateSha256}");

            return signingScope;
        }

        private static string GetAndroidSigningConfigPath()
        {
            var configPath = GetArg(AndroidSigningConfigArg);
            if (string.IsNullOrWhiteSpace(configPath))
                configPath = Environment.GetEnvironmentVariable(AndroidSigningConfigEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(configPath))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userProfile))
                    userProfile = Environment.GetEnvironmentVariable("USERPROFILE");

                configPath = Path.Combine(userProfile, DefaultAndroidSigningConfigRelativePath);
            }

            return Path.GetFullPath(configPath);
        }

        private static string ResolveConfiguredPath(string path, string configDirectory)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(configDirectory, path));
        }

        private static void RequireConfigValue(string value, string fieldName, string configPath)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Android signing config field '{fieldName}' is missing: {configPath}");
        }

        private static void WriteSummary(
            string outputRoot,
            string buildPath,
            BuildReport report,
            bool development,
            bool showDevelopmentConsole,
            AndroidSigningScope androidSigning)
        {
            var summary = report.summary;
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"result\": \"{Escape(summary.result.ToString())}\",");
            builder.AppendLine($"  \"platform\": \"{Escape(summary.platform.ToString())}\",");
            builder.AppendLine($"  \"outputPath\": \"{Escape(buildPath)}\",");
            builder.AppendLine($"  \"totalSize\": {summary.totalSize},");
            builder.AppendLine($"  \"totalTime\": \"{Escape(summary.totalTime.ToString())}\",");
            builder.AppendLine($"  \"development\": {development.ToString().ToLowerInvariant()},");
            builder.AppendLine(
                $"  \"developmentConsoleVisible\": {showDevelopmentConsole.ToString().ToLowerInvariant()},");
            builder.AppendLine($"  \"androidSigningConfigured\": {(androidSigning != null).ToString().ToLowerInvariant()},");
            builder.AppendLine($"  \"androidSigningKeyAlias\": {JsonStringOrNull(androidSigning?.KeyAliasName)},");
            builder.AppendLine($"  \"androidSigningCertificateSha256\": {JsonStringOrNull(androidSigning?.CertificateSha256)}");
            builder.AppendLine("}");

            File.WriteAllText(Path.Combine(outputRoot, $"build-summary-unity-{summary.platform}.json"), builder.ToString());
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string JsonStringOrNull(string value)
        {
            return string.IsNullOrEmpty(value) ? "null" : $"\"{Escape(value)}\"";
        }

        [Serializable]
        private sealed class AndroidSigningConfig
        {
            public string keystorePath;
            public string keystorePass;
            public string keyaliasName;
            public string keyaliasPass;
            public string certificateSha256;
        }

        private sealed class AndroidSigningScope : IDisposable
        {
            private readonly bool _previousUseCustomKeystore;
            private readonly string _previousKeystoreName;
            private readonly string _previousKeystorePass;
            private readonly string _previousKeyaliasName;
            private readonly string _previousKeyaliasPass;
            private bool _disposed;

            private AndroidSigningScope(AndroidSigningConfig config, string configPath, string keystorePath)
            {
                ConfigPath = configPath;
                KeyAliasName = config.keyaliasName;
                CertificateSha256 = config.certificateSha256;

                _previousUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
                _previousKeystoreName = PlayerSettings.Android.keystoreName;
                _previousKeystorePass = PlayerSettings.Android.keystorePass;
                _previousKeyaliasName = PlayerSettings.Android.keyaliasName;
                _previousKeyaliasPass = PlayerSettings.Android.keyaliasPass;

                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = config.keystorePass;
                PlayerSettings.Android.keyaliasName = config.keyaliasName;
                PlayerSettings.Android.keyaliasPass = config.keyaliasPass;
            }

            public string ConfigPath { get; }
            public string KeyAliasName { get; }
            public string CertificateSha256 { get; }

            public static AndroidSigningScope Apply(AndroidSigningConfig config, string configPath, string keystorePath)
            {
                return new AndroidSigningScope(config, configPath, keystorePath);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                PlayerSettings.Android.useCustomKeystore = _previousUseCustomKeystore;
                PlayerSettings.Android.keystoreName = _previousKeystoreName;
                PlayerSettings.Android.keystorePass = _previousKeystorePass;
                PlayerSettings.Android.keyaliasName = _previousKeyaliasName;
                PlayerSettings.Android.keyaliasPass = _previousKeyaliasPass;
            }
        }
    }
}
#endif
