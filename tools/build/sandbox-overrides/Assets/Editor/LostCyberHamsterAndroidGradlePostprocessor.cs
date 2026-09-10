#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    public sealed class LostCyberHamsterAndroidGradlePostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var gradleRoot = FindGradleRoot(path);
            if (string.IsNullOrEmpty(gradleRoot))
            {
                Debug.LogWarning($"Gradle root was not found for generated Android project: {path}");
                return;
            }

            // Ограничиваем одновременную упаковку ассетов внутри общего heap Gradle.
            WriteProperty(Path.Combine(gradleRoot, "gradle.properties"), "org.gradle.workers.max", "2");
            WriteProperty(Path.Combine(gradleRoot, "gradle.properties"), "org.gradle.parallel", "false");

            var cmakeDir = GetUnityBundledCmakeDir();
            if (!Directory.Exists(cmakeDir))
            {
                Debug.LogWarning($"Unity bundled CMake directory was not found: {cmakeDir}");
                return;
            }

            WriteProperty(Path.Combine(gradleRoot, "local.properties"), "cmake.dir", cmakeDir);
        }

        private static string FindGradleRoot(string path)
        {
            var directory = new DirectoryInfo(path);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "settings.gradle")) ||
                    File.Exists(Path.Combine(directory.FullName, "local.properties")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        private static string GetUnityBundledCmakeDir()
        {
            var editorDir = Path.GetDirectoryName(EditorApplication.applicationPath);
            return Path.Combine(
                editorDir,
                "Data",
                "PlaybackEngines",
                "AndroidPlayer",
                "SDK",
                "cmake",
                "3.22.1");
        }

        private static void WriteProperty(string propertiesPath, string key, string value)
        {
            var escapedValue = value.Replace("\\", "\\\\").Replace(":", "\\:");
            var lines = File.Exists(propertiesPath)
                ? File.ReadAllLines(propertiesPath)
                    .Where(line => !line.StartsWith($"{key}=", StringComparison.Ordinal))
                    .ToList()
                : new List<string>();

            lines.Add($"{key}={escapedValue}");
            File.WriteAllLines(propertiesPath, lines);
            Debug.Log($"Configured Android Gradle {key}: {value}");
        }
    }
}
#endif
