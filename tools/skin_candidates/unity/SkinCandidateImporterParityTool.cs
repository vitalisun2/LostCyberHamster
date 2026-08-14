#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Копирует importer contract исходных sprite sheets в candidate assets через Unity API.
/// </summary>
public static class SkinCandidateImporterParityTool
{
    private const string SourceRootArgument = "-skinSourceRoot";
    private const string CandidateRootArgument = "-skinCandidateRoot";
    private const string SheetsArgument = "-skinSheets";
    private const string LogPrefix = "[SkinCandidateImporterParity]";

    /// <summary>
    /// Запускает перенос importer settings, sprite rects, pivots и custom physics shapes.
    /// </summary>
    public static void Run()
    {
        // Читаем и проверяем общий contract запуска.
        var arguments = Environment.GetCommandLineArgs();
        var sourceRoot = NormalizeAssetRoot(GetRequiredArgument(arguments, SourceRootArgument));
        var candidateRoot = NormalizeAssetRoot(GetRequiredArgument(arguments, CandidateRootArgument));
        if (string.Equals(sourceRoot, candidateRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Source and candidate roots must differ.");

        ValidateAssetRoot(sourceRoot, "source");
        ValidateAssetRoot(candidateRoot, "candidate");
        var sheetNames = ResolveSheetNames(arguments, sourceRoot, candidateRoot);

        // Обрабатываем все sheets и собираем полный список ошибок.
        var configuredCount = 0;
        var failures = new List<string>();
        foreach (var sheetName in sheetNames)
        {
            var sourcePath = $"{sourceRoot}/{sheetName}";
            var candidatePath = $"{candidateRoot}/{sheetName}";
            try
            {
                ConfigureSheet(sourcePath, candidatePath);
                configuredCount++;
            }
            catch (Exception exception)
            {
                failures.Add($"{sheetName}: {exception.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        // Пишем batch summary и возвращаем failure через executeMethod exception.
        var summary =
            $"{LogPrefix} configured={configuredCount}, failed={failures.Count}, " +
            $"source={sourceRoot}, candidate={candidateRoot}";
        if (failures.Count > 0)
        {
            var details = string.Join(Environment.NewLine, failures);
            Debug.LogError($"{summary}{Environment.NewLine}{details}");
            throw new InvalidOperationException($"Skin candidate importer parity failed.{Environment.NewLine}{details}");
        }

        Debug.Log(summary);
    }

    /// <summary>
    /// Копирует texture settings, platform settings и Sprite Editor data одного sheet.
    /// </summary>
    private static void ConfigureSheet(string sourcePath, string candidatePath)
    {
        // Сначала применяем TextureImporter contract и завершаем reimport.
        ValidatePngAsset(sourcePath, "source");
        ValidatePngAsset(candidatePath, "candidate");

        var sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        var candidateImporter = AssetImporter.GetAtPath(candidatePath) as TextureImporter;
        if (sourceImporter == null || candidateImporter == null)
        {
            throw new InvalidOperationException(
                $"TextureImporter unavailable: {sourcePath} -> {candidatePath}");
        }

        CopyTextureSettings(sourceImporter, candidateImporter);
        candidateImporter.SaveAndReimport();

        // После reimport заново получаем importers и переносим Sprite Editor data.
        sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        candidateImporter = AssetImporter.GetAtPath(candidatePath) as TextureImporter;
        if (sourceImporter == null || candidateImporter == null)
            throw new InvalidOperationException($"TextureImporter lost after reimport: {candidatePath}");

        CopySpriteData(sourceImporter, candidateImporter);
        AssetDatabase.ImportAsset(candidatePath, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>
    /// Копирует общий TextureImporter contract и ровно набор source platform settings.
    /// </summary>
    private static void CopyTextureSettings(
        TextureImporter sourceImporter,
        TextureImporter candidateImporter)
    {
        // Копируем общий importer contract.
        var textureSettings = new TextureImporterSettings();
        sourceImporter.ReadTextureSettings(textureSettings);
        candidateImporter.SetTextureSettings(textureSettings);

        candidateImporter.textureType = sourceImporter.textureType;
        candidateImporter.textureShape = sourceImporter.textureShape;
        candidateImporter.spriteImportMode = sourceImporter.spriteImportMode;
        candidateImporter.spritePixelsPerUnit = sourceImporter.spritePixelsPerUnit;
        candidateImporter.filterMode = sourceImporter.filterMode;
        candidateImporter.anisoLevel = sourceImporter.anisoLevel;
        candidateImporter.mipMapBias = sourceImporter.mipMapBias;
        candidateImporter.wrapMode = sourceImporter.wrapMode;
        candidateImporter.wrapModeU = sourceImporter.wrapModeU;
        candidateImporter.wrapModeV = sourceImporter.wrapModeV;
        candidateImporter.wrapModeW = sourceImporter.wrapModeW;
        candidateImporter.mipmapEnabled = sourceImporter.mipmapEnabled;
        candidateImporter.alphaSource = sourceImporter.alphaSource;
        candidateImporter.alphaIsTransparency = sourceImporter.alphaIsTransparency;
        candidateImporter.sRGBTexture = sourceImporter.sRGBTexture;
        candidateImporter.npotScale = sourceImporter.npotScale;
        candidateImporter.isReadable = sourceImporter.isReadable;
        candidateImporter.streamingMipmaps = sourceImporter.streamingMipmaps;
        candidateImporter.streamingMipmapsPriority = sourceImporter.streamingMipmapsPriority;
        candidateImporter.maxTextureSize = sourceImporter.maxTextureSize;
        candidateImporter.textureCompression = sourceImporter.textureCompression;
        candidateImporter.compressionQuality = sourceImporter.compressionQuality;
        candidateImporter.crunchedCompression = sourceImporter.crunchedCompression;

        // Удаляем лишние candidate platforms и копируем source platform list.
        var sourcePlatforms = ReadPlatformNames(sourceImporter.assetPath + ".meta");
        var candidatePlatforms = ReadPlatformNames(candidateImporter.assetPath + ".meta");
        foreach (var platformName in candidatePlatforms.Except(sourcePlatforms, StringComparer.Ordinal))
        {
            if (!string.Equals(platformName, "DefaultTexturePlatform", StringComparison.Ordinal))
                candidateImporter.ClearPlatformTextureSettings(platformName);
        }

        foreach (var platformName in sourcePlatforms)
        {
            var platformSettings = sourceImporter.GetPlatformTextureSettings(platformName);
            candidateImporter.SetPlatformTextureSettings(platformSettings);
        }
    }

    /// <summary>
    /// Копирует Sprite Editor data с новыми candidate GUID и точными custom outlines.
    /// </summary>
    private static void CopySpriteData(
        TextureImporter sourceImporter,
        TextureImporter candidateImporter)
    {
        // Получаем data providers и проверяем source metadata.
        var factories = new SpriteDataProviderFactories();
        factories.Init();

        var sourceProvider = factories.GetSpriteEditorDataProviderFromObject(sourceImporter);
        var candidateProvider = factories.GetSpriteEditorDataProviderFromObject(candidateImporter);
        if (sourceProvider == null || candidateProvider == null)
            throw new InvalidOperationException("Sprite Editor Data Provider unavailable.");

        sourceProvider.InitSpriteEditorDataProvider();
        candidateProvider.InitSpriteEditorDataProvider();

        var sourceRects = sourceProvider.GetSpriteRects();
        if (sourceRects == null || sourceRects.Length == 0)
            throw new InvalidOperationException($"Source has no SpriteRects: {sourceImporter.assetPath}");

        var expectedPhysicsVertexCounts =
            ReadPhysicsShapeVertexCounts(sourceImporter.assetPath + ".meta");
        if (expectedPhysicsVertexCounts.Count != sourceRects.Length)
        {
            throw new InvalidOperationException(
                $"Physics metadata sprite count {expectedPhysicsVertexCounts.Count} " +
                $"does not match SpriteRect count {sourceRects.Length}: {sourceImporter.assetPath}");
        }

        var sourcePhysics = sourceProvider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
        var candidatePhysics = candidateProvider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
        var sourceOutline = sourceProvider.GetDataProvider<ISpriteOutlineDataProvider>();
        var candidateOutline = candidateProvider.GetDataProvider<ISpriteOutlineDataProvider>();
        var candidateNames = candidateProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (sourcePhysics == null || candidatePhysics == null ||
            sourceOutline == null || candidateOutline == null || candidateNames == null)
        {
            throw new InvalidOperationException("Required Sprite Editor data provider unavailable.");
        }

        // Строим candidate rects и outlines с новыми sprite GUID.
        var candidateRects = new SpriteRect[sourceRects.Length];
        var idPairs = new List<SpriteNameFileIdPair>(sourceRects.Length);
        var physicsById = new List<(GUID id, List<Vector2[]> outlines)>(sourceRects.Length);
        var outlineById = new List<(GUID id, List<Vector2[]> outlines)>(sourceRects.Length);

        for (var index = 0; index < sourceRects.Length; index++)
        {
            var sourceRect = sourceRects[index];
            var candidateId = GUID.Generate();
            candidateRects[index] = new SpriteRect
            {
                name = sourceRect.name,
                rect = sourceRect.rect,
                border = sourceRect.border,
                alignment = sourceRect.alignment,
                pivot = sourceRect.pivot,
                spriteID = candidateId
            };
            idPairs.Add(new SpriteNameFileIdPair(sourceRect.name, candidateId));

            var expectedCounts = expectedPhysicsVertexCounts[index];
            var physicsShapes = CloneOutlines(
                sourcePhysics.GetOutlines(sourceRect.spriteID),
                expectedCounts);
            physicsById.Add((candidateId, physicsShapes));

            var outlines = CloneOutlines(sourceOutline.GetOutlines(sourceRect.spriteID));
            outlineById.Add((candidateId, outlines));
        }

        // Применяем весь Sprite Editor contract одной транзакцией provider-а.
        candidateProvider.SetSpriteRects(candidateRects);
        candidateNames.SetNameFileIdPairs(idPairs);
        foreach (var entry in physicsById)
            candidatePhysics.SetOutlines(entry.id, entry.outlines);
        foreach (var entry in outlineById)
            candidateOutline.SetOutlines(entry.id, entry.outlines);
        candidateProvider.Apply();
    }

    /// <summary>
    /// Клонирует outlines и обрезает Unity 6.2 trailing garbage по source `.meta`.
    /// </summary>
    private static List<Vector2[]> CloneOutlines(
        List<Vector2[]> outlines,
        int[] expectedVertexCounts = null)
    {
        if (outlines == null)
            return new List<Vector2[]>();

        if (expectedVertexCounts == null)
            return outlines.Select(SanitizeShape).ToList();

        // Physics shapes обрезаем только до serialized source count.
        var exact = new List<Vector2[]>(expectedVertexCounts.Length);
        for (var shapeIndex = 0; shapeIndex < expectedVertexCounts.Length; shapeIndex++)
        {
            var sourceShape = shapeIndex < outlines.Count && outlines[shapeIndex] != null
                ? outlines[shapeIndex]
                : Array.Empty<Vector2>();
            var expectedCount = expectedVertexCounts[shapeIndex];
            if (sourceShape.Length < expectedCount)
            {
                throw new InvalidOperationException(
                    $"Physics outline has {sourceShape.Length} vertices, expected {expectedCount}.");
            }

            exact.Add(sourceShape.Take(expectedCount).ToArray());
        }

        return exact;
    }

    /// <summary>
    /// Удаляет невалидные provider-точки из обычных sprite outlines.
    /// </summary>
    private static Vector2[] SanitizeShape(Vector2[] outline)
    {
        if (outline == null)
            return Array.Empty<Vector2>();

        return outline
            .Where(point =>
                float.IsFinite(point.x) &&
                float.IsFinite(point.y) &&
                Mathf.Abs(point.x) < 10000f &&
                Mathf.Abs(point.y) < 10000f)
            .ToArray();
    }

    /// <summary>
    /// Читает точное число physics vertices каждого sprite из serialized source `.meta`.
    /// </summary>
    private static List<int[]> ReadPhysicsShapeVertexCounts(string sourceMetaAssetPath)
    {
        // Читаем source `.meta`, а не потенциально повреждённый provider buffer.
        var absolutePath = ToAbsolutePath(sourceMetaAssetPath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Source .meta unavailable.", absolutePath);

        var result = new List<int[]>();
        var currentShapes = new List<int>();
        var inSpriteSheet = false;
        var inSprite = false;
        var inPhysicsShape = false;

        // Для каждого sprite считаем vertices каждой serialized physics shape.
        foreach (var line in File.ReadLines(absolutePath))
        {
            if (line == "  spriteSheet:")
            {
                inSpriteSheet = true;
                continue;
            }

            if (!inSpriteSheet)
                continue;
            if (line == "    outline:")
                break;

            if (line == "    - serializedVersion: 2")
            {
                if (inSprite)
                    result.Add(currentShapes.ToArray());
                currentShapes = new List<int>();
                inSprite = true;
                inPhysicsShape = false;
                continue;
            }

            if (!inSprite)
                continue;
            if (line.StartsWith("      physicsShape:", StringComparison.Ordinal))
            {
                inPhysicsShape = !line.TrimEnd().EndsWith("[]", StringComparison.Ordinal);
                continue;
            }

            if (!inPhysicsShape)
                continue;
            if (line.StartsWith("      tessellationDetail:", StringComparison.Ordinal))
            {
                inPhysicsShape = false;
                continue;
            }

            if (line.StartsWith("      - - {", StringComparison.Ordinal))
            {
                currentShapes.Add(1);
            }
            else if (line.StartsWith("        - {", StringComparison.Ordinal) && currentShapes.Count > 0)
            {
                currentShapes[currentShapes.Count - 1]++;
            }
        }

        if (inSprite)
            result.Add(currentShapes.ToArray());
        return result;
    }

    /// <summary>
    /// Читает serialized platform list и переводит YAML build target в Unity API name.
    /// </summary>
    private static IReadOnlyList<string> ReadPlatformNames(string metaAssetPath)
    {
        // Serialized list нужен для удаления лишних candidate platform entries.
        var absolutePath = ToAbsolutePath(metaAssetPath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Texture .meta unavailable.", absolutePath);

        var platformNames = new List<string>();
        var inPlatformSettings = false;
        // iOS YAML target соответствует iPhone имени TextureImporter API.
        foreach (var line in File.ReadLines(absolutePath))
        {
            if (line == "  platformSettings:")
            {
                inPlatformSettings = true;
                continue;
            }

            if (!inPlatformSettings)
                continue;
            if (line == "  spriteSheet:")
                break;
            const string Prefix = "    buildTarget: ";
            if (!line.StartsWith(Prefix, StringComparison.Ordinal))
                continue;

            var serializedName = line.Substring(Prefix.Length).Trim();
            var apiName = string.Equals(serializedName, "iOS", StringComparison.Ordinal)
                ? "iPhone"
                : serializedName;
            if (!string.IsNullOrWhiteSpace(apiName) && !platformNames.Contains(apiName))
                platformNames.Add(apiName);
        }

        if (platformNames.Count == 0)
            throw new InvalidOperationException($"No platform settings found: {metaAssetPath}");
        return platformNames;
    }

    /// <summary>
    /// Возвращает explicit sheet list или top-level candidate PNG с matching source.
    /// </summary>
    private static IReadOnlyList<string> ResolveSheetNames(
        string[] arguments,
        string sourceRoot,
        string candidateRoot)
    {
        var explicitValue = GetOptionalArgument(arguments, SheetsArgument);
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            // Explicit list сохраняет заданный пользователем порядок.
            var explicitNames = explicitValue
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (explicitNames.Length == 0)
                throw new ArgumentException($"{SheetsArgument} contains no sheet names.");
            foreach (var sheetName in explicitNames)
                ValidateSheetName(sheetName);
            return explicitNames;
        }

        // Discovery берёт только top-level PNG с source-файлом того же имени.
        var candidateDirectory = ToAbsolutePath(candidateRoot);
        var discovered = Directory
            .EnumerateFiles(candidateDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => File.Exists(ToAbsolutePath($"{sourceRoot}/{name}")))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (discovered.Length == 0)
        {
            throw new InvalidOperationException(
                $"No top-level candidate PNG with matching source found: {candidateRoot}");
        }

        return discovered;
    }

    /// <summary>
    /// Проверяет asset-relative root без выхода за Unity Assets.
    /// </summary>
    private static string NormalizeAssetRoot(string value)
    {
        var normalized = value.Trim().Replace('\\', '/').TrimEnd('/');
        if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
            normalized.Contains("../", StringComparison.Ordinal) ||
            normalized.Contains("/..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Root must be an Assets/... path: {value}");
        }

        return normalized;
    }

    /// <summary>
    /// Проверяет наличие Unity asset folder.
    /// </summary>
    private static void ValidateAssetRoot(string assetRoot, string role)
    {
        if (!AssetDatabase.IsValidFolder(assetRoot) || !Directory.Exists(ToAbsolutePath(assetRoot)))
            throw new DirectoryNotFoundException($"{role} root unavailable: {assetRoot}");
    }

    /// <summary>
    /// Проверяет PNG и его импорт в AssetDatabase.
    /// </summary>
    private static void ValidatePngAsset(string assetPath, string role)
    {
        if (!File.Exists(ToAbsolutePath(assetPath)))
            throw new FileNotFoundException($"{role} PNG unavailable.", assetPath);
        if (AssetImporter.GetAtPath(assetPath) == null)
            throw new InvalidOperationException($"{role} asset is not imported: {assetPath}");
    }

    /// <summary>
    /// Проверяет top-level PNG filename.
    /// </summary>
    private static void ValidateSheetName(string sheetName)
    {
        if (!string.Equals(Path.GetFileName(sheetName), sheetName, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(sheetName), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Sheet must be a top-level .png filename: {sheetName}");
        }
    }

    /// <summary>
    /// Возвращает обязательный command-line argument.
    /// </summary>
    private static string GetRequiredArgument(string[] arguments, string name)
    {
        var value = GetOptionalArgument(arguments, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required argument: {name} <Assets/...>");
        return value;
    }

    /// <summary>
    /// Возвращает значение command-line argument.
    /// </summary>
    private static string GetOptionalArgument(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            if (!string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException($"Argument requires value: {name}");
            return arguments[index + 1];
        }

        return null;
    }

    /// <summary>
    /// Преобразует Unity asset path в абсолютный filesystem path.
    /// </summary>
    private static string ToAbsolutePath(string assetPath)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
#endif
