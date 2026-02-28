#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Automatically configures import settings for static (non-animated) obstacle sprites.
    /// 
    /// Detection logic:
    /// - Path matches Content/locations/*/sprites/obstacle_*.png
    /// - Filename does NOT end with _idle or _walk (those are animated sprite sheets
    ///   managed by <see cref="ObstacleAnimationImporter"/>)
    /// - No matching animation clip exists in Assets/Animations/Obstacles/
    ///   (e.g. obstacle_new_york_dog.png is skipped because obstacle_new_york_dog_idle.anim exists)
    /// 
    /// Applied settings:
    /// - SpriteImportMode.Single — no manual Sprite Editor slicing needed
    /// - Pivot: Bottom Center (0.5, 0) — matches game's pivot convention
    /// - TextureType: Sprite (2D and UI)
    /// - MipmapEnabled: false
    /// - FilterMode: Point (pixel art)
    /// - PixelsPerUnit: 100
    /// </summary>
    public class ObstacleSpritePostprocessor : AssetPostprocessor
    {
        private const float PivotX = 0.5f;
        private const float PivotY = 0f; // Bottom Center

        private void OnPreprocessTexture()
        {
            if (!IsStaticObstacleSprite(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = 100;

            // Set pivot to Bottom Center via TextureImporterSettings
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(PivotX, PivotY);
            importer.SetTextureSettings(settings);

            Debug.Log($"[ObstacleSpritePostprocessor] Configured static obstacle sprite: {assetPath} " +
                      $"(Single mode, Bottom Center pivot)");
        }

        /// <summary>
        /// Determines if the asset at the given path is a static (non-animated) obstacle sprite.
        /// Static obstacle sprites are PNG files in Content/locations/*/sprites/
        /// that start with "obstacle_" and don't have animation suffixes (_idle, _walk).
        /// </summary>
        private static bool IsStaticObstacleSprite(string assetPath)
        {
            // Must be a PNG in locations sprites folder
            if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Normalized path uses forward slashes in Unity
            if (!assetPath.Contains("Content/locations/") || !assetPath.Contains("/sprites/"))
                return false;

            var fileName = Path.GetFileNameWithoutExtension(assetPath);

            // Must be an obstacle sprite
            if (!fileName.StartsWith("obstacle_", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Animated sprite sheets are managed by ObstacleAnimationImporter — skip them
            // Check 1: filename ends with _idle/_walk (sprite sheet named after animation type)
            if (fileName.EndsWith("_idle", System.StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("_walk", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Check 2: a matching .anim clip exists (sprite sheet named after obstacle,
            // e.g. obstacle_new_york_dog.png → obstacle_new_york_dog_idle.anim)
            if (HasMatchingAnimationClip(fileName))
                return false;

            return true;
        }

        /// <summary>
        /// Checks if an animation clip exists for the given sprite sheet base name.
        /// Uses file-system check against Assets/Animations/Obstacles/ directory.
        /// </summary>
        private static bool HasMatchingAnimationClip(string baseName)
        {
            // Path relative to project root (LostCyberHamster/)
            var animDir = Path.Combine("Assets", "Animations", "Obstacles");

            if (File.Exists(Path.Combine(animDir, $"{baseName}_idle.anim")) ||
                File.Exists(Path.Combine(animDir, $"{baseName}_walk.anim")))
                return true;

            return false;
        }
    }
}
#endif
