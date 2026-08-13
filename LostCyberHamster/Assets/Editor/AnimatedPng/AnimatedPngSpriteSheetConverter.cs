#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Assets.EditorTools.AnimatedPng
{
    /// <summary>
    /// Пакетно конвертирует выбранные APNG в нарезанные grid sprite sheets и сохраняет исходники рядом.
    /// </summary>
    internal static class AnimatedPngSpriteSheetConverter
    {
        private const string MenuPath = "Assets/Convert Animated PNG to Sprite Sheet";
        private const string SourcePostfix = " Animated PNG";
        private const int MaxTextureSize = 2048;

        [MenuItem(MenuPath, false, 2000)]
        private static void ConvertSelectedAssets()
        {
            var selectedFiles = GetSelectedAnimatedPngs();
            if (selectedFiles.Count == 0)
            {
                Debug.LogError("[AnimatedPngConverter] Среди выбранных файлов нет APNG-анимаций.");
                return;
            }

            var convertedAssets = new List<UnityEngine.Object>(selectedFiles.Count);
            var failedCount = 0;

            // Каждый файл конвертируется независимо, ошибка одного не останавливает пакет.
            foreach (var selectedFile in selectedFiles)
            {
                if (TryConvertAsset(selectedFile.AssetPath, selectedFile.Bytes, out var convertedAsset))
                    convertedAssets.Add(convertedAsset);
                else
                    failedCount++;
            }

            // Оставляем успешно созданные sprite sheets выделенными в Project View.
            if (convertedAssets.Count > 0)
            {
                Selection.objects = convertedAssets.ToArray();
                EditorGUIUtility.PingObject(convertedAssets[0]);
            }

            Debug.Log($"[AnimatedPngConverter] Пакет завершён. Создано: {convertedAssets.Count}, " +
                      $"ошибок: {failedCount}.");
        }

        private static bool TryConvertAsset(string sourceAssetPath, byte[] sourceBytes, out Texture2D convertedAsset)
        {
            convertedAsset = null;

            var directory = Path.GetDirectoryName(sourceAssetPath)?.Replace('\\', '/');
            var baseName = Path.GetFileNameWithoutExtension(sourceAssetPath);
            var archivedAssetPath = $"{directory}/{baseName}{SourcePostfix}.png";
            if (AssetDatabase.LoadMainAssetAtPath(archivedAssetPath) != null || File.Exists(GetAbsolutePath(archivedAssetPath)))
            {
                Debug.LogError($"[AnimatedPngConverter] Архивный файл уже существует: {archivedAssetPath}");
                return false;
            }

            Texture2D spriteSheet = null;
            var sourceMoved = false;

            try
            {
                // Полностью декодируем APNG до переименования исходного asset.
                var animation = AnimatedPngDecoder.Decode(sourceBytes);
                var layout = CalculateLayout(animation.Frames.Count, animation.Width, animation.Height);
                spriteSheet = CreateSpriteSheet(animation, layout);
                var sheetBytes = spriteSheet.EncodeToPNG();
                var importSettings = TextureImportSettings.Capture(sourceAssetPath);

                // Сохраняем GUID исходника за архивным APNG и создаём новый PNG по старому пути.
                var moveError = AssetDatabase.MoveAsset(sourceAssetPath, archivedAssetPath);
                if (!string.IsNullOrEmpty(moveError))
                    throw new InvalidOperationException(moveError);
                sourceMoved = true;

                File.WriteAllBytes(GetAbsolutePath(sourceAssetPath), sheetBytes);
                AssetDatabase.ImportAsset(sourceAssetPath, ImportAssetOptions.ForceUpdate);
                ConfigureImporter(sourceAssetPath, baseName, animation, layout, importSettings);

                var result = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
                convertedAsset = result;
                Debug.Log($"[AnimatedPngConverter] Создан sprite sheet: {sourceAssetPath}, " +
                          $"кадров: {animation.Frames.Count}, сетка: {layout.Columns}x{layout.Rows}. " +
                          $"Исходник: {archivedAssetPath}");
                return true;
            }
            catch (Exception exception)
            {
                // Возвращаем исходный asset и его GUID при ошибке после переименования.
                if (sourceMoved)
                {
                    AssetDatabase.DeleteAsset(sourceAssetPath);
                    var rollbackError = AssetDatabase.MoveAsset(archivedAssetPath, sourceAssetPath);
                    if (!string.IsNullOrEmpty(rollbackError))
                        Debug.LogError($"[AnimatedPngConverter] Не удалось вернуть исходник: {rollbackError}");
                }

                Debug.LogError($"[AnimatedPngConverter] Конвертация не выполнена: {exception.Message}");
                return false;
            }
            finally
            {
                if (spriteSheet != null)
                    UnityEngine.Object.DestroyImmediate(spriteSheet);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateSelectedAsset()
        {
            return GetSelectedAnimatedPngs().Count > 0;
        }

        private static List<SelectedAnimatedPng> GetSelectedAnimatedPngs()
        {
            var selectedFiles = new List<SelectedAnimatedPng>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var selectedObject in Selection.objects)
            {
                var assetPath = AssetDatabase.GetAssetPath(selectedObject);
                if (!uniquePaths.Add(assetPath) || !TryGetAnimatedPngBytes(assetPath, out var bytes, false))
                    continue;

                selectedFiles.Add(new SelectedAnimatedPng(assetPath, bytes));
            }

            return selectedFiles
                .OrderBy(file => file.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryGetAnimatedPngBytes(string assetPath, out byte[] bytes, bool logError = true)
        {
            bytes = null;
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                bytes = File.ReadAllBytes(GetAbsolutePath(assetPath));
                if (AnimatedPngDecoder.IsAnimated(bytes))
                    return true;
            }
            catch (Exception exception)
            {
                if (logError)
                    Debug.LogError($"[AnimatedPngConverter] Не удалось прочитать файл: {exception.Message}");
                return false;
            }

            if (logError)
                Debug.LogError("[AnimatedPngConverter] Выбранный PNG не содержит APNG-анимацию.");
            return false;
        }

        private static SheetLayout CalculateLayout(int frameCount, int frameWidth, int frameHeight)
        {
            var maxColumns = Math.Min(frameCount, MaxTextureSize / frameWidth);
            var maxRows = MaxTextureSize / frameHeight;
            if (maxColumns == 0 || maxRows == 0 || maxColumns * maxRows < frameCount)
            {
                throw new InvalidOperationException(
                    $"Кадры {frameWidth}x{frameHeight} не помещаются в sprite sheet {MaxTextureSize}x{MaxTextureSize}.");
            }

            SheetLayout best = default;
            var bestArea = long.MaxValue;
            var bestLongestSide = int.MaxValue;

            // Ищем компактную сетку, которая не требует resize при импорте Unity.
            for (var columns = 1; columns <= maxColumns; columns++)
            {
                var rows = Mathf.CeilToInt(frameCount / (float)columns);
                if (rows > maxRows)
                    continue;

                var width = columns * frameWidth;
                var height = rows * frameHeight;
                var longestSide = Math.Max(width, height);
                var area = (long)width * height;
                if (area < bestArea || area == bestArea && longestSide < bestLongestSide)
                {
                    best = new SheetLayout(columns, rows, width, height);
                    bestLongestSide = longestSide;
                    bestArea = area;
                }
            }

            return best;
        }

        private static Texture2D CreateSpriteSheet(AnimatedPngDecoder.DecodedAnimation animation, SheetLayout layout)
        {
            var sheetPixels = new Color32[layout.Width * layout.Height];

            // Кадры идут слева направо, затем сверху вниз.
            for (var frameIndex = 0; frameIndex < animation.Frames.Count; frameIndex++)
            {
                var column = frameIndex % layout.Columns;
                var rowFromTop = frameIndex / layout.Columns;
                var destinationX = column * animation.Width;
                var destinationY = (layout.Rows - rowFromTop - 1) * animation.Height;
                var framePixels = animation.Frames[frameIndex];

                for (var y = 0; y < animation.Height; y++)
                {
                    Array.Copy(
                        framePixels,
                        y * animation.Width,
                        sheetPixels,
                        (destinationY + y) * layout.Width + destinationX,
                        animation.Width);
                }
            }

            var sheet = new Texture2D(layout.Width, layout.Height, TextureFormat.RGBA32, false);
            sheet.SetPixels32(sheetPixels);
            sheet.Apply(false, false);
            return sheet;
        }

        private static void ConfigureImporter(
            string assetPath,
            string baseName,
            AnimatedPngDecoder.DecodedAnimation animation,
            SheetLayout layout,
            TextureImportSettings settings)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"TextureImporter не найден: {assetPath}");

            // Переносим визуальные настройки исходника и включаем Multiple без mipmaps.
            settings.Apply(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxTextureSize;
            importer.SaveAndReimport();

            // Записываем slices через актуальный Unity Sprite Editor Data Provider API.
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null)
                throw new InvalidOperationException("Sprite Editor Data Provider недоступен.");
            dataProvider.InitSpriteEditorDataProvider();

            var spriteRects = new SpriteRect[animation.Frames.Count];
            var nameFileIdPairs = new List<SpriteNameFileIdPair>(animation.Frames.Count);
            for (var frameIndex = 0; frameIndex < animation.Frames.Count; frameIndex++)
            {
                var column = frameIndex % layout.Columns;
                var rowFromTop = frameIndex / layout.Columns;
                var spriteId = GUID.Generate();
                var spriteName = $"{baseName}_{frameIndex}";
                spriteRects[frameIndex] = new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(
                        column * animation.Width,
                        (layout.Rows - rowFromTop - 1) * animation.Height,
                        animation.Width,
                        animation.Height),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = spriteId
                };
                nameFileIdPairs.Add(new SpriteNameFileIdPair(spriteName, spriteId));
            }

            dataProvider.SetSpriteRects(spriteRects);
            var nameProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameProvider?.SetNameFileIdPairs(nameFileIdPairs);
            dataProvider.Apply();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string GetAbsolutePath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private readonly struct SheetLayout
        {
            public SheetLayout(int columns, int rows, int width, int height)
            {
                Columns = columns;
                Rows = rows;
                Width = width;
                Height = height;
            }

            public int Columns { get; }
            public int Rows { get; }
            public int Width { get; }
            public int Height { get; }
        }

        private readonly struct SelectedAnimatedPng
        {
            public SelectedAnimatedPng(string assetPath, byte[] bytes)
            {
                AssetPath = assetPath;
                Bytes = bytes;
            }

            public string AssetPath { get; }
            public byte[] Bytes { get; }
        }

        private readonly struct TextureImportSettings
        {
            private TextureImportSettings(TextureImporter importer)
            {
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                FilterMode = importer.filterMode;
                WrapMode = importer.wrapMode;
                PixelsPerUnit = importer.spritePixelsPerUnit;
                MeshType = textureSettings.spriteMeshType;
                TextureCompression = importer.textureCompression;
                CompressionQuality = importer.compressionQuality;
                SrgbTexture = importer.sRGBTexture;
                AlphaIsTransparency = importer.alphaIsTransparency;
            }

            private FilterMode FilterMode { get; }
            private TextureWrapMode WrapMode { get; }
            private float PixelsPerUnit { get; }
            private SpriteMeshType MeshType { get; }
            private TextureImporterCompression TextureCompression { get; }
            private int CompressionQuality { get; }
            private bool SrgbTexture { get; }
            private bool AlphaIsTransparency { get; }

            public static TextureImportSettings Capture(string assetPath)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException($"TextureImporter не найден: {assetPath}");
                return new TextureImportSettings(importer);
            }

            public void Apply(TextureImporter importer)
            {
                importer.filterMode = FilterMode;
                importer.wrapMode = WrapMode;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.textureCompression = TextureCompression;
                importer.compressionQuality = CompressionQuality;
                importer.sRGBTexture = SrgbTexture;
                importer.alphaIsTransparency = AlphaIsTransparency;

                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = MeshType;
                importer.SetTextureSettings(textureSettings);
            }
        }
    }
}
#endif
