#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Читает default sprite-sheet schema, проверяет sources и клонирует importer.
    /// </summary>
    internal static class SkinSpriteSheetAuthoring
    {
        /// <summary>
        /// Возвращает фактический список default sprite sheets режима.
        /// </summary>
        public static IReadOnlyList<string> GetTemplateSheets(
            SkinData defaultSkin,
            bool isSkateboard)
        {
            string root = GetTemplateRoot(defaultSkin, isSkateboard);
            if (!AssetDatabase.IsValidFolder(root))
            {
                throw new InvalidOperationException(
                    $"Default sprite folder is missing: {root}.");
            }

            List<string> paths = AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    path.StartsWith(root + "/", StringComparison.Ordinal) &&
                    string.Equals(
                        Path.GetExtension(path),
                        ".png",
                        StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (paths.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Default sprite folder has no PNG sprite sheets: {root}.");
            }

            foreach (string path in paths)
                ValidateTemplateImporter(path);
            return paths;
        }

        /// <summary>
        /// Возвращает относительные имена sheets для Add Skin window.
        /// </summary>
        public static IReadOnlyList<string> GetExpectedRelativePaths(
            SkinData defaultSkin,
            bool isSkateboard)
        {
            string root = GetTemplateRoot(defaultSkin, isSkateboard);
            return GetTemplateSheets(defaultSkin, isSkateboard)
                .Select(path => GetRelativeAssetPath(root, path))
                .ToList();
        }

        /// <summary>
        /// Проверяет точное соответствие source folder default schema и PNG размерам.
        /// </summary>
        public static string ValidateSourceFolder(
            string sourceFolder,
            SkinData defaultSkin,
            bool isSkateboard,
            IReadOnlyList<string> templateSheets)
        {
            string modeName = isSkateboard ? "Skateboard" : "Normal";
            if (string.IsNullOrWhiteSpace(sourceFolder))
            {
                throw new InvalidOperationException(
                    $"{modeName} source folder is required.");
            }

            // Нормализуем выбранный filesystem path без проектных допущений.
            string fullSourceFolder;
            try
            {
                fullSourceFolder = Path.GetFullPath(sourceFolder.Trim());
                string pathRoot = Path.GetPathRoot(fullSourceFolder);
                if (!string.Equals(
                        fullSourceFolder,
                        pathRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    fullSourceFolder = fullSourceFolder.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{modeName} source folder path is invalid: " +
                    exception.Message);
            }

            if (!Directory.Exists(fullSourceFolder))
            {
                throw new InvalidOperationException(
                    $"{modeName} source folder does not exist: " +
                    fullSourceFolder);
            }

            // Сопоставляем полный относительный PNG path с default schema.
            string templateRoot = GetTemplateRoot(
                defaultSkin,
                isSkateboard);
            List<string> expectedPaths = templateSheets
                .Select(path => GetRelativeAssetPath(templateRoot, path))
                .ToList();
            Dictionary<string, string> sourcePaths = GetSourcePngPaths(
                fullSourceFolder,
                modeName);
            ValidatePathSet(modeName, expectedPaths, sourcePaths);

            // Одинаковый canvas гарантирует применимость template slicing.
            // Каждый target asset получает новый GUID и default importer metadata.
            foreach (string templatePath in templateSheets)
            {
                string relativePath = GetRelativeAssetPath(
                    templateRoot,
                    templatePath);
                Vector2Int templateSize = ReadPngSize(
                    FileUtil.GetPhysicalPath(templatePath),
                    $"default sheet '{relativePath}'");
                Vector2Int sourceSize = ReadPngSize(
                    sourcePaths[relativePath],
                    $"{modeName} sheet '{relativePath}'");
                if (sourceSize != templateSize)
                {
                    throw new InvalidOperationException(
                        $"{modeName} sheet '{relativePath}' has size " +
                        $"{sourceSize.x}x{sourceSize.y}; expected " +
                        $"{templateSize.x}x{templateSize.y}.");
                }
            }

            return fullSourceFolder;
        }

        /// <summary>
        /// Копирует sheets и importer metadata соответствующих default assets.
        /// </summary>
        public static void CopySheets(
            string sourceFolder,
            SkinData defaultSkin,
            string slug,
            bool isSkateboard,
            IReadOnlyList<string> templateSheets)
        {
            string templateRoot = GetTemplateRoot(
                defaultSkin,
                isSkateboard);
            string targetRoot = SkinVisualContentLayout.GetSpritePath(
                slug,
                isSkateboard);
            foreach (string templatePath in templateSheets)
            {
                string relativePath = GetRelativeAssetPath(
                    templateRoot,
                    templatePath);
                string targetPath = $"{targetRoot}/{relativePath}";
                EnsureFolder(GetParentAssetPath(targetPath));

                if (!AssetDatabase.CopyAsset(templatePath, targetPath))
                {
                    throw new InvalidOperationException(
                        $"Cannot clone default importer for '{relativePath}'.");
                }

                string sourcePath = Path.Combine(
                    sourceFolder,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                File.Copy(
                    sourcePath,
                    FileUtil.GetPhysicalPath(targetPath),
                    overwrite: true);
                AssetDatabase.ImportAsset(
                    targetPath,
                    ImportAssetOptions.ForceUpdate);
                ValidateImportedSheet(templatePath, targetPath);
            }
        }

        private static string GetTemplateRoot(
            SkinData defaultSkin,
            bool isSkateboard)
        {
            if (defaultSkin == null)
                throw new ArgumentNullException(nameof(defaultSkin));

            string address = isSkateboard
                ? defaultSkin.SkateboardSkinVisualAddress
                : defaultSkin.SkinVisualAddress;
            string slug = SkinVisualContentLayout.GetSlug(address);
            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new InvalidOperationException(
                    "Default skin visual slug is missing.");
            }

            return SkinVisualContentLayout.GetSpritePath(
                slug,
                isSkateboard);
        }

        private static void ValidateTemplateImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath)
                as TextureImporter;
            int spriteCount = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .Count();
            if (importer == null ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                spriteCount == 0)
            {
                throw new InvalidOperationException(
                    $"Default sheet is not a sliced multi-sprite texture: " +
                    assetPath);
            }
        }

        private static Dictionary<string, string> GetSourcePngPaths(
            string sourceFolder,
            string modeName)
        {
            try
            {
                return Directory.EnumerateFiles(
                        sourceFolder,
                        "*",
                        SearchOption.AllDirectories)
                    .Where(path => string.Equals(
                        Path.GetExtension(path),
                        ".png",
                        StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        path => Path.GetRelativePath(sourceFolder, path)
                            .Replace('\\', '/'),
                        path => path,
                        StringComparer.Ordinal);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Cannot read {modeName} source folder: " +
                    exception.Message);
            }
        }

        private static void ValidatePathSet(
            string modeName,
            IReadOnlyList<string> expectedPaths,
            IReadOnlyDictionary<string, string> sourcePaths)
        {
            var errors = new List<string>();
            foreach (string expectedPath in expectedPaths)
            {
                if (sourcePaths.ContainsKey(expectedPath))
                    continue;

                string differentCase = sourcePaths.Keys.FirstOrDefault(path =>
                    string.Equals(
                        path,
                        expectedPath,
                        StringComparison.OrdinalIgnoreCase));
                errors.Add(differentCase == null
                    ? $"missing '{expectedPath}'"
                    : $"'{differentCase}' must match casing '{expectedPath}'");
            }

            foreach (string sourcePath in sourcePaths.Keys.Where(path =>
                         expectedPaths.All(expected => !string.Equals(
                             expected,
                             path,
                             StringComparison.OrdinalIgnoreCase))))
            {
                errors.Add($"unexpected PNG '{sourcePath}'");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{modeName} source folder does not match default " +
                    $"sprite-sheet structure:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static Vector2Int ReadPngSize(
            string path,
            string label)
        {
            Texture2D texture = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                    throw new InvalidDataException("file is empty");

                texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    mipChain: false);
                if (!ImageConversion.LoadImage(
                        texture,
                        bytes,
                        markNonReadable: true))
                {
                    throw new InvalidDataException("PNG cannot be decoded");
                }

                return new Vector2Int(texture.width, texture.height);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Cannot use {label}: {exception.Message}");
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateImportedSheet(
            string templatePath,
            string targetPath)
        {
            TextureImporter targetImporter = AssetImporter.GetAtPath(
                targetPath) as TextureImporter;
            List<string> templateNames = AssetDatabase.LoadAllAssetsAtPath(
                    templatePath)
                .OfType<Sprite>()
                .Select(sprite => sprite.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            List<string> targetNames = AssetDatabase.LoadAllAssetsAtPath(
                    targetPath)
                .OfType<Sprite>()
                .Select(sprite => sprite.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            if (targetImporter == null ||
                !templateNames.SequenceEqual(targetNames))
            {
                throw new InvalidOperationException(
                    $"Imported sheet does not preserve default slicing: " +
                    targetPath);
            }
        }

        private static string GetRelativeAssetPath(
            string root,
            string assetPath)
        {
            string prefix = root.TrimEnd('/') + "/";
            if (!assetPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Asset '{assetPath}' is outside '{root}'.");
            }

            return assetPath[prefix.Length..];
        }

        private static string GetParentAssetPath(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex > 0
                ? assetPath[..separatorIndex]
                : string.Empty;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = GetParentAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidOperationException(
                    $"Invalid asset folder path: {assetPath}.");
            }

            EnsureFolder(parent);
            string name = assetPath[(parent.Length + 1)..];
            if (string.IsNullOrWhiteSpace(
                    AssetDatabase.CreateFolder(parent, name)))
            {
                throw new InvalidOperationException(
                    $"Cannot create asset folder: {assetPath}.");
            }
        }
    }
}
#endif
