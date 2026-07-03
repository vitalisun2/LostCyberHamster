#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    public static class AddressablesAutomation
    {
        public static void RebuildPlayerContent()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    Fail("AddressableAssetSettings not found.");
                    return;
                }

                var builder = settings.ActivePlayerDataBuilder;
                Debug.Log(
                    $"[ADDRESSABLES AUTOMATION] Rebuild started. " +
                    $"builder={builder?.Name ?? "<none>"} buildTarget={EditorUserBuildSettings.activeBuildTarget}");

                AddressableAssetSettings.CleanPlayerContent(builder);
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    Fail(result.Error);
                    return;
                }

                Debug.Log(
                    $"[ADDRESSABLES AUTOMATION] Rebuild completed. " +
                    $"duration={result.Duration:F2}s outputPath={result.OutputPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Fail($"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[ADDRESSABLES AUTOMATION] Rebuild failed. {message}");
            EditorApplication.Exit(1);
        }
    }
}
#endif
