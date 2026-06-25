#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Keeps scrolling environment textures compatible with 4x4 block compression while allowing art-driven height.
    /// </summary>
    public sealed class EnvironmentTexturePostprocessor : AssetPostprocessor
    {
        private const int CompressionBlockSize = 4;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            PadImportedEnvironmentTextures(importedAssets);
            PadImportedEnvironmentTextures(movedAssets);
        }

        private static void PadImportedEnvironmentTextures(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                if (!IsEnvironmentTexture(assetPath))
                {
                    continue;
                }

                PadHeightForEtc2IfNeeded(assetPath);
            }
        }

        private static bool IsEnvironmentTexture(string assetPath)
        {
            return assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                   && assetPath.IndexOf("Assets/Content/locations/", StringComparison.Ordinal) >= 0
                   && assetPath.IndexOf("/sprites/backgrounds/", StringComparison.Ordinal) >= 0;
        }

        private static void PadHeightForEtc2IfNeeded(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(fullPath)))
            {
                UnityEngine.Object.DestroyImmediate(source);
                Debug.LogWarning($"[EnvironmentTexturePostprocessor] Failed to read PNG: {assetPath}");
                return;
            }

            var originalHeight = source.height;
            var paddedHeight = RoundUpToCompressionBlock(source.height);
            if (paddedHeight == source.height)
            {
                UnityEngine.Object.DestroyImmediate(source);
                return;
            }

            var padded = new Texture2D(source.width, paddedHeight, TextureFormat.RGBA32, false);
            var transparentPixels = new Color32[source.width * paddedHeight];

            padded.SetPixels32(transparentPixels);
            padded.SetPixels32(0, 0, source.width, source.height, source.GetPixels32());
            padded.Apply();

            File.WriteAllBytes(fullPath, padded.EncodeToPNG());
            PreservePivotAfterTopPadding(assetPath, originalHeight, paddedHeight);

            Debug.Log(
                $"[EnvironmentTexturePostprocessor] Increased '{assetPath}' height from {originalHeight} to {paddedHeight} for ETC2 4x4 block compatibility.");

            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(padded);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static int RoundUpToCompressionBlock(int value)
        {
            var remainder = value % CompressionBlockSize;
            return remainder == 0 ? value : value + CompressionBlockSize - remainder;
        }

        private static void PreservePivotAfterTopPadding(string assetPath, int originalHeight, int paddedHeight)
        {
            if (originalHeight <= 0 || paddedHeight <= 0)
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Single)
            {
                return;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            var pivot = settings.spritePivot;
            pivot.y = Mathf.Clamp01(pivot.y * originalHeight / paddedHeight);

            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;

            importer.SetTextureSettings(settings);
        }
    }
}
#endif
