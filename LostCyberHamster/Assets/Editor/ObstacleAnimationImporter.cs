#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Автоматический импорт анимаций препятствий из PNG-последовательностей.
    /// 
    /// <para><b>Workflow художника:</b></para>
    /// <list type="number">
    /// <item>Создать анимацию в Procreate с правильным таймингом (дублировать кадры для пауз)</item>
    /// <item>Экспортировать PNG sequence с именем: obstacle_{location}_{category}_{id}_{animType}-{frame}.png</item>
    /// <item>Скопировать PNG в: C:\Dropbox\exchange\crystal_wave\sprites\sprites_for_animation</item>
    /// <item>В Unity: Tools → Obstacle Animations → Import From Dropbox</item>
    /// </list>
    /// 
    /// <para><b>Naming Convention:</b></para>
    /// <code>
    /// obstacle_new_york_people_1_idle-1.png
    /// obstacle_new_york_people_1_idle-2.png  ← дубликат (пауза)
    /// obstacle_new_york_people_1_idle-3.png  ← дубликат (пауза)
    /// obstacle_new_york_people_1_idle-4.png
    /// ...
    /// 
    /// Результат:
    /// - Спрайтшит: Assets/Content/Locations/NewYork/Sprites/obstacle_new_york_people_1_idle.png (только уникальные)
    /// - Анимация: Assets/Animations/Obstacles/obstacle_new_york_people_1_idle.anim (с правильным таймингом)
    /// </code>
    /// 
    /// <para><b>Оптимизация:</b></para>
    /// Система автоматически удаляет дубликаты кадров из спрайтшита, сохраняя тайминг в AnimationClip.
    /// Экономия памяти GPU: 50-80% для анимаций с паузами.
    /// 
    /// <para><b>Важно:</b></para>
    /// <list type="bullet">
    /// <item>Каждый персонаж имеет только одну анимацию одного типа (_idle ИЛИ _walk)</item>
    /// <item>Множественные движения создаются через дублирование кадров, не через отдельные файлы</item>
    /// <item>Исходные PNG удаляются из Dropbox после успешного импорта</item>
    /// <item>Pivot спрайтов: Bottom Center (0.5, 0.0)</item>
    /// </list>
    /// </summary>
    public static class ObstacleAnimationImporter
    {
        private const string SourceFolder = @"C:\Dropbox\exchange\crystal_wave\sprites\sprites_for_animation";
        private const string ContentLocationsRoot = "Assets/Content/locations";
        private const string AnimationsFolder = "Assets/Animations/Obstacles";
        private const float PivotX = 0.5f;
        private const float PivotY = 0f;

        private const int FrameRate = 5; // Procreate export setting: 5 FPS

        [MenuItem("Tools/Obstacle Animations/Import From Dropbox", priority = 500)]
        [MenuItem("Assets/Obstacle Animations/Import From Dropbox", priority = 10)]
        public static void ImportFromDropboxMenu()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[ObstacleAnimationImporter] Cannot run while in Play Mode.");
                return;
            }

            ImportInternal();
        }

        private static void ImportInternal()
        {
            if (!Directory.Exists(SourceFolder))
            {
                Debug.LogWarning($"[ObstacleAnimationImporter] Source folder not found: {SourceFolder}");
                return;
            }

            var pngPaths = Directory.GetFiles(SourceFolder, "*.png", SearchOption.TopDirectoryOnly);
            Debug.Log($"[ObstacleAnimationImporter] Found {pngPaths.Length} png files in source folder.");

            var groups = GroupFrames(pngPaths);
            Debug.Log($"[ObstacleAnimationImporter] Prepared {groups.Count} groups to process.");

            foreach (var (baseName, frames) in groups.OrderBy(g => g.Key))
            {
                ProcessGroup(baseName, frames);
            }
        }

        private static Dictionary<string, List<FrameInfo>> GroupFrames(IEnumerable<string> pngPaths)
        {
            var result = new Dictionary<string, List<FrameInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in pngPaths)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var lastDash = fileName.LastIndexOf('-');
                if (lastDash <= 0 || lastDash >= fileName.Length - 1)
                {
                    Debug.LogWarning($"[ObstacleAnimationImporter] Skipping file with invalid name (no index): {fileName}");
                    continue;
                }

                var baseName = fileName[..lastDash];
                var indexPart = fileName[(lastDash + 1)..];
                if (!int.TryParse(indexPart, out var index) || index <= 0)
                {
                    Debug.LogWarning($"[ObstacleAnimationImporter] Skipping file with non-numeric index: {fileName}");
                    continue;
                }

                if (!result.TryGetValue(baseName, out var list))
                {
                    list = new List<FrameInfo>();
                    result[baseName] = list;
                }

                // Hash will be computed during texture loading
                list.Add(new FrameInfo(baseName, index, path, string.Empty));
            }

            foreach (var list in result.Values)
            {
                list.Sort((a, b) => a.Index.CompareTo(b.Index));
            }

            return result;
        }

        private static void ProcessGroup(string baseName, List<FrameInfo> frames)
        {
            if (!TryExtractLocation(baseName, out var locationPascal))
            {
                Debug.LogError($"[ObstacleAnimationImporter] Could not extract location from '{baseName}'. Group skipped.");
                return;
            }

            var locationFolder = ResolveLocationFolderName(locationPascal);

            if (!TryLoadFrames(frames, out var loadedFrames, out var frameWidth, out var frameHeight))
            {
                Debug.LogError($"[ObstacleAnimationImporter] Frame size mismatch in '{baseName}'. Group skipped.");
                return;
            }

            // Validate and optionally resize sprites to match expected dimensions
            if (!ValidateAndResizeSpriteSize(baseName, ref frameWidth, ref frameHeight, ref loadedFrames))
            {
                CleanupTextures(loadedFrames);
                return;
            }

            try
            {
                // Deduplicate frames to optimize sprite sheet size
                var dedupResult = DeduplicateFrames(loadedFrames);
                var optimizationPercent = dedupResult.UniqueFrames.Count < loadedFrames.Count
                    ? (1.0f - (float)dedupResult.UniqueFrames.Count / loadedFrames.Count) * 100f
                    : 0f;

                if (dedupResult.UniqueFrames.Count < loadedFrames.Count)
                {
                    Debug.Log($"[ObstacleAnimationImporter] Optimized '{baseName}': {loadedFrames.Count} frames → {dedupResult.UniqueFrames.Count} unique ({optimizationPercent:F1}% reduction)");
                }

                if (!TryCreateSpriteSheet(baseName, dedupResult.UniqueFrames, frameWidth, frameHeight, locationFolder, out var spriteSheetAssetPath))
                {
                    return;
                }

                if (!TryConfigureImporter(baseName, dedupResult.UniqueFrames, frameWidth, frameHeight, spriteSheetAssetPath))
                {
                    return;
                }

                var sprites = LoadSprites(spriteSheetAssetPath);
                if (sprites.Count == 0)
                {
                    Debug.LogError($"[ObstacleAnimationImporter] No sprites found after import for '{baseName}'. Group skipped.");
                    return;
                }

                var animationCreated = TryCreateAnimation(baseName, dedupResult.FrameSequence, sprites);
                if (animationCreated)
                {
                    Debug.Log($"[ObstacleAnimationImporter] Animation created: Assets/Animations/Obstacles/{baseName}.anim ({dedupResult.FrameSequence.Count} keyframes)");
                }

                if (!animationCreated && AnimationExists(baseName))
                {
                    Debug.LogWarning($"[ObstacleAnimationImporter] Animation already exists, skipping creation: Assets/Animations/Obstacles/{baseName}.anim");
                }

                if (animationCreated || AnimationExists(baseName))
                {
                    DeleteSourceFrames(baseName, frames);
                }
            }
            finally
            {
                CleanupTextures(loadedFrames);
            }
        }

        private static bool TryLoadFrames(List<FrameInfo> frames, out List<LoadedFrame> loadedFrames, out int width, out int height)
        {
            loadedFrames = new List<LoadedFrame>(frames.Count);
            width = 0;
            height = 0;

            foreach (var frame in frames)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var bytes = File.ReadAllBytes(frame.FilePath);
                if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: false))
                {
                    Debug.LogError($"[ObstacleAnimationImporter] Failed to load image: {frame.FilePath}");
                    return false;
                }

                if (loadedFrames.Count == 0)
                {
                    width = texture.width;
                    height = texture.height;
                }
                else if (texture.width != width || texture.height != height)
                {
                    Debug.LogError($"[ObstacleAnimationImporter] Size mismatch in group '{frame.BaseName}'. Expected {width}x{height}, got {texture.width}x{texture.height}.");
                    texture.DestroyImmediate();
                    CleanupTextures(loadedFrames);
                    return false;
                }

                loadedFrames.Add(new LoadedFrame(frame, texture));
            }

            return true;
        }

        private static bool TryCreateSpriteSheet(string baseName, List<LoadedFrame> frames, int frameWidth, int frameHeight, string locationFolder, out string spriteSheetAssetPath)
        {
            spriteSheetAssetPath = $"{ContentLocationsRoot}/{locationFolder}/sprites/{baseName}.png";
            var spriteSheetFullPath = GetAbsolutePath(spriteSheetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(spriteSheetFullPath) ?? spriteSheetFullPath);

            try
            {
                var sheet = new Texture2D(frameWidth * frames.Count, frameHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };

                for (var i = 0; i < frames.Count; i++)
                {
                    var pixels = frames[i].Texture.GetPixels();
                    sheet.SetPixels(frameWidth * i, 0, frameWidth, frameHeight, pixels);
                }

                sheet.Apply();

                var png = sheet.EncodeToPNG();
                if (File.Exists(spriteSheetFullPath))
                {
                    Debug.LogWarning($"[ObstacleAnimationImporter] Overwriting existing sprite sheet: {spriteSheetAssetPath}");
                }

                File.WriteAllBytes(spriteSheetFullPath, png);
                AssetDatabase.ImportAsset(spriteSheetAssetPath, ImportAssetOptions.ForceUpdate);

                Debug.Log($"[ObstacleAnimationImporter] Sprite sheet created: {spriteSheetAssetPath} ({frames.Count} frames)");
                sheet.DestroyImmediate();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ObstacleAnimationImporter] Failed to create sprite sheet for '{baseName}': {ex.Message}");
                return false;
            }
        }

        private static bool TryConfigureImporter(string baseName, List<LoadedFrame> uniqueFrames, int frameWidth, int frameHeight, string spriteSheetAssetPath)
        {
            var importer = AssetImporter.GetAtPath(spriteSheetAssetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ObstacleAnimationImporter] TextureImporter not found for '{spriteSheetAssetPath}'.");
                return false;
            }

            var metas = new SpriteMetaData[uniqueFrames.Count];
            for (var i = 0; i < uniqueFrames.Count; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = $"{baseName}-{i}",
                    rect = new Rect(frameWidth * i, 0, frameWidth, frameHeight),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(PivotX, PivotY)
                };
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.spritesheet = metas;
            importer.SaveAndReimport();

            return true;
        }

        private static List<Sprite> LoadSprites(string spriteSheetAssetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(spriteSheetAssetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryCreateAnimation(string baseName, List<int> frameSequence, IReadOnlyCollection<Sprite> sprites)
        {
            var animAssetPath = $"{AnimationsFolder}/{baseName}.anim";
            var animFullPath = GetAbsolutePath(animAssetPath);
            if (File.Exists(animFullPath))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(animFullPath) ?? animFullPath);

                var spriteLookup = sprites.ToDictionary(s => s.name, s => s, StringComparer.OrdinalIgnoreCase);
                var clip = new AnimationClip { frameRate = FrameRate };

                var keyframes = new ObjectReferenceKeyframe[frameSequence.Count];
                for (var i = 0; i < frameSequence.Count; i++)
                {
                    var uniqueIndex = frameSequence[i];
                    var spriteName = $"{baseName}-{uniqueIndex}";
                    if (!spriteLookup.TryGetValue(spriteName, out var sprite))
                    {
                        Debug.LogError($"[ObstacleAnimationImporter] Sprite not found for keyframe '{spriteName}' in '{baseName}'.");
                        return false;
                    }

                    keyframes[i] = new ObjectReferenceKeyframe
                    {
                        time = i / (float)FrameRate,
                        value = sprite
                    };
                }

                var binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = string.Empty,
                    propertyName = "m_Sprite"
                };

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
                SetLoopTime(clip, true);

                AssetDatabase.CreateAsset(clip, animAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(animAssetPath, ImportAssetOptions.ForceUpdate);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ObstacleAnimationImporter] Failed to create animation for '{baseName}': {ex.Message}");
                return false;
            }
        }

        private static bool AnimationExists(string baseName)
        {
            var animAssetPath = $"{AnimationsFolder}/{baseName}.anim";
            var animFullPath = GetAbsolutePath(animAssetPath);
            return File.Exists(animFullPath);
        }

        private static void DeleteSourceFrames(string baseName, IEnumerable<FrameInfo> frames)
        {
            foreach (var frame in frames)
            {
                try
                {
                    File.Delete(frame.FilePath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ObstacleAnimationImporter] Failed to delete '{frame.FilePath}' for '{baseName}': {ex.Message}");
                }
            }

            Debug.Log($"[ObstacleAnimationImporter] Source frames deleted for '{baseName}'.");
        }

        private static bool TryExtractLocation(string baseName, out string locationPascal)
        {
            var parts = baseName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var obstacleIndex = Array.FindIndex(parts, p => string.Equals(p, "obstacle", StringComparison.OrdinalIgnoreCase));
            var peopleIndex = Array.FindIndex(parts, p => string.Equals(p, "people", StringComparison.OrdinalIgnoreCase));

            if (obstacleIndex < 0 || peopleIndex < 0 || peopleIndex - obstacleIndex <= 1)
            {
                locationPascal = string.Empty;
                return false;
            }

            var locationParts = parts.Skip(obstacleIndex + 1).Take(peopleIndex - obstacleIndex - 1);
            var snake = string.Join("_", locationParts);
            locationPascal = ToPascalCaseFromSnake(snake);
            return !string.IsNullOrEmpty(locationPascal);
        }

        private static string ResolveLocationFolderName(string locationPascal)
        {
            if (!Directory.Exists(ContentLocationsRoot))
            {
                return locationPascal;
            }

            var normalizedTarget = NormalizeLocationKey(locationPascal);
            foreach (var dir in Directory.GetDirectories(ContentLocationsRoot))
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName))
                {
                    continue;
                }

                if (NormalizeLocationKey(folderName) == normalizedTarget)
                {
                    return folderName;
                }
            }

            return locationPascal;
        }

        private static string NormalizeLocationKey(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsLetter(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        private static string ToPascalCaseFromSnake(string value)
        {
            var segments = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    continue;
                }

                var lower = segment.ToLowerInvariant();
                sb.Append(char.ToUpperInvariant(lower[0]));
                if (lower.Length > 1)
                {
                    sb.Append(lower[1..]);
                }
            }

            return sb.ToString();
        }

        private static string GetAbsolutePath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static void SetLoopTime(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void CleanupTextures(IEnumerable<LoadedFrame> frames)
        {
            foreach (var frame in frames)
            {
                frame.Texture.DestroyImmediate();
            }
        }

        /// <summary>
        /// Computes a hash for a texture by reading all pixel data.
        /// Used to detect duplicate frames for optimization.
        /// </summary>
        private static string ComputeTextureHash(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            
            unchecked
            {
                int hash = 17;
                foreach (var pixel in pixels)
                {
                    hash = hash * 31 + pixel.r;
                    hash = hash * 31 + pixel.g;
                    hash = hash * 31 + pixel.b;
                    hash = hash * 31 + pixel.a;
                }
                return hash.ToString();
            }
        }
        
        /// <summary>
        /// Compares two textures pixel-by-pixel for exact equality.
        /// </summary>
        private static bool AreTexturesEqual(Texture2D a, Texture2D b)
        {
            if (a.width != b.width || a.height != b.height)
                return false;
                
            var pixelsA = a.GetPixels32();
            var pixelsB = b.GetPixels32();
            
            if (pixelsA.Length != pixelsB.Length)
                return false;
                
            for (int i = 0; i < pixelsA.Length; i++)
            {
                if (pixelsA[i].r != pixelsB[i].r ||
                    pixelsA[i].g != pixelsB[i].g ||
                    pixelsA[i].b != pixelsB[i].b ||
                    pixelsA[i].a != pixelsB[i].a)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Deduplicates frames by content hash and pixel comparison, keeping only unique textures.
        /// Returns unique frames and a sequence mapping original indices to unique indices.
        /// </summary>
        private static DeduplicationResult DeduplicateFrames(List<LoadedFrame> frames)
        {
            var uniqueFrames = new List<LoadedFrame>();
            var frameSequence = new List<int>();
            var hashToIndices = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var hash = ComputeTextureHash(frame.Texture);
                
                int uniqueIndex = -1;
                
                // Check if we've seen this hash before
                if (hashToIndices.TryGetValue(hash, out var candidateIndices))
                {
                    // Hash collision possible - verify with pixel-perfect comparison
                    foreach (var candidateIdx in candidateIndices)
                    {
                        if (AreTexturesEqual(frame.Texture, uniqueFrames[candidateIdx].Texture))
                        {
                            uniqueIndex = candidateIdx;
                            break;
                        }
                    }
                }
                
                // If not a duplicate, add as new unique frame
                if (uniqueIndex == -1)
                {
                    uniqueIndex = uniqueFrames.Count;
                    uniqueFrames.Add(frame);
                    
                    if (!hashToIndices.ContainsKey(hash))
                    {
                        hashToIndices[hash] = new List<int>();
                    }
                    hashToIndices[hash].Add(uniqueIndex);
                }

                frameSequence.Add(uniqueIndex);
            }

            return new DeduplicationResult(uniqueFrames, frameSequence);
        }

        /// <summary>
        /// Validates and optionally resizes sprites to match expected dimensions.
        /// 
        /// Smart Resize Rules:
        /// 1. Downscale only (no upscaling - prevents quality loss)
        /// 2. Same aspect ratio (no stretching)
        /// 3. Clean integer ratio (2x, 4x, 8x - preserves pixel structure)
        /// 4. Result divisible by 4 (ETC2 compression requirement)
        /// 
        /// If source sprites are at higher resolution with correct aspect ratio,
        /// they will be automatically downscaled to target size.
        /// </summary>
        private static bool ValidateAndResizeSpriteSize(string baseName, ref int width, ref int height, ref List<LoadedFrame> frames)
        {
            int expectedWidth, expectedHeight;
            string categoryName;
            
            // Determine expected size based on naming convention
            var lowerName = baseName.ToLowerInvariant();
            
            if (lowerName.Contains("_people_"))
            {
                expectedWidth = Assets.Scripts.Consts.BIG_ALIVE_WIDTH;
                expectedHeight = Assets.Scripts.Consts.BIG_ALIVE_HEIGHT;
                categoryName = "bigAlive/people";
            }
            else if (lowerName.Contains("_dog_") || lowerName.Contains("_cat_"))
            {
                expectedWidth = Assets.Scripts.Consts.SMALL_ALIVE_WIDTH;
                expectedHeight = Assets.Scripts.Consts.SMALL_ALIVE_HEIGHT;
                categoryName = "smallAlive";
            }
            else if (lowerName.Contains("_car_") || lowerName.Contains("_truck_") || lowerName.Contains("_bus_"))
            {
                expectedWidth = Assets.Scripts.Consts.BIG_NOTALIVE_WIDTH;
                expectedHeight = Assets.Scripts.Consts.BIG_NOTALIVE_HEIGHT;
                categoryName = "bigNotAlive/vehicle";
            }
            else
            {
                // Unknown category - skip validation
                Debug.LogWarning($"[ObstacleAnimationImporter] Could not determine expected size for '{baseName}'. " +
                                 $"Sprite size: {width}x{height}. Make sure this matches your intended obstacle type.");
                return true;
            }
            
            // Check if already correct size
            if (width == expectedWidth && height == expectedHeight)
            {
                return true;
            }
            
            // Try smart resize
            if (TrySmartResize(baseName, categoryName, ref width, ref height, expectedWidth, expectedHeight, ref frames))
            {
                return true;
            }
            
            // Resize failed - show error
            Debug.LogError($"[ObstacleAnimationImporter] Sprite size validation failed for '{baseName}'.\\n" +
                           $"Expected: {expectedWidth}x{expectedHeight} ({categoryName})\\n" +
                           $"Got: {width}x{height}\\n" +
                           $"Cannot auto-resize: aspect ratio or scale ratio is invalid.\\n" +
                           $"Please resize sprites in Procreate to exact dimensions before exporting.");
            return false;
        }
        
        /// <summary>
        /// Attempts to intelligently resize sprites with strict validation rules.
        /// Only allows downscaling with clean integer ratios and matching aspect ratios.
        /// </summary>
        private static bool TrySmartResize(
            string baseName, 
            string categoryName,
            ref int currentWidth, 
            ref int currentHeight, 
            int targetWidth, 
            int targetHeight,
            ref List<LoadedFrame> frames)
        {
            float scaleX = (float)targetWidth / currentWidth;
            float scaleY = (float)targetHeight / currentHeight;
            
            // Rule 1: Downscale only (no upscaling)
            if (scaleX > 1f || scaleY > 1f)
            {
                Debug.LogError($"[ObstacleAnimationImporter] Cannot upscale '{baseName}'. Source resolution too small.\\n" +
                               $"Source: {currentWidth}x{currentHeight} → Target: {targetWidth}x{targetHeight}");
                return false;
            }
            
            // Rule 2: Same aspect ratio (prevent distortion)
            if (Math.Abs(scaleX - scaleY) > 0.01f)
            {
                Debug.LogError($"[ObstacleAnimationImporter] Aspect ratio mismatch for '{baseName}'.\\n" +
                               $"Source: {currentWidth}x{currentHeight} → Target: {targetWidth}x{targetHeight}\\n" +
                               $"Scale: {scaleX:F2}x vs {scaleY:F2}x (must be equal)");
                return false;
            }
            
            float scale = scaleX; // Both are equal
            
            // Rule 3: Clean integer ratio (2x, 4x, 8x for best quality)
            float inverseScale = 1f / scale; // e.g., 0.5 → 2x, 0.25 → 4x
            bool isCleanRatio = Math.Abs(inverseScale - Math.Round(inverseScale)) < 0.01f;
            
            if (!isCleanRatio)
            {
                Debug.LogError($"[ObstacleAnimationImporter] Non-integer scale ratio for '{baseName}'.\\n" +
                               $"Source: {currentWidth}x{currentHeight} → Target: {targetWidth}x{targetHeight}\\n" +
                               $"Scale: {1/scale:F2}x (must be 2x, 4x, 8x, etc.)\\n" +
                               $"Please draw at exact multiple of target size.");
                return false;
            }
            
            // Rule 4: Result must be divisible by 4 (ETC2 requirement - should already be true)
            if (targetWidth % 4 != 0 || targetHeight % 4 != 0)
            {
                Debug.LogError($"[ObstacleAnimationImporter] Target size not divisible by 4: {targetWidth}x{targetHeight}");
                return false;
            }
            
            // All checks passed - perform resize
            Debug.Log($"[ObstacleAnimationImporter] Auto-resizing '{baseName}' ({categoryName}):\\n" +
                      $"{currentWidth}x{currentHeight} → {targetWidth}x{targetHeight} ({inverseScale:F0}x downscale)");
            
            var resizedFrames = new List<LoadedFrame>(frames.Count);
            
            foreach (var frame in frames)
            {
                var resized = ResizeTexture(frame.Texture, targetWidth, targetHeight, FilterMode.Point);
                resizedFrames.Add(new LoadedFrame(frame.Info, resized));
                
                // Clean up original
                frame.Texture.DestroyImmediate();
            }
            
            frames = resizedFrames;
            currentWidth = targetWidth;
            currentHeight = targetHeight;
            
            return true;
        }
        
        /// <summary>
        /// Resizes a texture using Point filtering (no anti-aliasing) for pixel-perfect downscaling.
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight, FilterMode filterMode)
        {
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = filterMode;
            
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            
            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }

        private readonly struct FrameInfo
        {
            public FrameInfo(string baseName, int index, string filePath, string hash)
            {
                BaseName = baseName;
                Index = index;
                FilePath = filePath;
                Hash = hash;
            }

            public string BaseName { get; }
            public int Index { get; }
            public string FilePath { get; }
            public string Hash { get; }
        }

        private readonly struct LoadedFrame
        {
            public LoadedFrame(FrameInfo info, Texture2D texture)
            {
                Info = info;
                Texture = texture;
            }

            public FrameInfo Info { get; }
            public Texture2D Texture { get; }
        }

        private readonly struct DeduplicationResult
        {
            public DeduplicationResult(List<LoadedFrame> uniqueFrames, List<int> frameSequence)
            {
                UniqueFrames = uniqueFrames;
                FrameSequence = frameSequence;
            }

            public List<LoadedFrame> UniqueFrames { get; }
            public List<int> FrameSequence { get; }
        }
    }

    internal static class TextureExtensions
    {
        public static void DestroyImmediate(this Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
