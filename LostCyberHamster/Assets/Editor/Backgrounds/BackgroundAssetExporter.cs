using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System.LevelManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Экспортирует рисунки и сохраняет новый либо повторно открытый Addressable-префаб.</summary>
    public static class BackgroundAssetExporter
    {
        /// <summary>Сохраняет композицию; при первичном экспорте также импортирует четыре рисунка.</summary>
        public static string Save(BackgroundAuthoringSession session)
        {
            // Проверяем черновик до изменения пользовательского контента.
            if (session == null || !session.IsActive)
                throw new InvalidOperationException("Черновая композиция закрыта.");
            session.UpdatePreview();
            session.ValidateForSave();
            session.Environment.ClearCopies();
            session.Environment.ValidateConfiguration();

            // Проверяем каталог Addressables до записи файлов.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("Настройки Addressables не найдены.");
            var groupName = $"scrolling_environment_{session.LocationSlug}";
            var group = settings.FindGroup(groupName);
            if (group == null)
                throw new InvalidOperationException($"Группа Addressables '{groupName}' не найдена.");
            var updatingPrefab = !string.IsNullOrEmpty(session.SavedPrefabPath);
            var environment = session.Environment;
            var originalSprites = BackgroundAuthoringSession.Roles.ToDictionary(role => role, role => environment.GetLayer(role).sprite);
            var imported = new Dictionary<EnvironmentLayerRole, Sprite>(originalSprites);
            var spriteDirectory = GetSpriteDirectory(session.LocationId);

            // Первичная подготовка создаёт PNG; повторная настройка использует импортированные спрайты.
            if (!updatingPrefab)
            {
                EnsureDirectory(spriteDirectory);
                EnsureDirectory(GetPrefabDirectory(session.LocationId));
                foreach (var role in BackgroundAuthoringSession.Roles)
                {
                    var name = GetSpriteName(role, session.LocationSlug, session.Daypart);
                    var path = $"{spriteDirectory}/{name}.png";
                    imported[role] = SaveSprite(path, name, session.GetTextureData(role));
                }
            }

            // Сохраняем только четыре авторских слоя, затем возвращаем черновые ссылки.
            var prefabName = EnvironmentKeyResolver.BuildEnvironmentKey(session.LocationId, session.Daypart);
            var prefabPath = updatingPrefab ? session.SavedPrefabPath : GetPrefabPath(session.LocationId, session.Daypart);
            environment.ClearCopies();
            try
            {
                foreach (var role in BackgroundAuthoringSession.Roles)
                    environment.GetLayer(role).sprite = imported[role];
                var prefab = PrefabUtility.SaveAsPrefabAsset(environment.gameObject, prefabPath, out var success);
                if (!success || prefab == null)
                    throw new InvalidOperationException($"Не удалось сохранить префаб: {prefabPath}");

                // Адреса соответствуют существующему каталогу окружений.
                if (!updatingPrefab)
                    foreach (var role in BackgroundAuthoringSession.Roles)
                    {
                        var name = GetSpriteName(role, session.LocationSlug, session.Daypart);
                        Register(settings, group, $"{spriteDirectory}/{name}.png", name);
                    }
                Register(settings, group, prefabPath, prefabName);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, group, true, true);
                EditorUtility.SetDirty(group);
                AssetDatabase.SaveAssets();
                return prefabPath;
            }
            finally
            {
                foreach (var role in BackgroundAuthoringSession.Roles)
                    if (environment != null)
                        environment.GetLayer(role).sprite = originalSprites[role];
                session.RefreshPreview();
            }
        }

        /// <summary>Возвращает каталог спрайтов фона внутри локации.</summary>
        public static string GetSpriteDirectory(string locationId) =>
            $"Assets/Content/locations/{locationId}/sprites/backgrounds";

        /// <summary>Возвращает каталог готовых окружений внутри локации.</summary>
        public static string GetPrefabDirectory(string locationId) =>
            $"Assets/Content/locations/{locationId}/prefabs/environments";

        /// <summary>Возвращает путь префаба по локации и времени суток.</summary>
        public static string GetPrefabPath(string locationId, string daypart) =>
            $"{GetPrefabDirectory(locationId)}/{EnvironmentKeyResolver.BuildEnvironmentKey(locationId, daypart)}.prefab";

        /// <summary>Формирует техническое имя рисунка по роли и варианту окружения.</summary>
        public static string GetSpriteName(EnvironmentLayerRole role, string locationSlug, string daypart)
        {
            var prefix = role switch
            {
                EnvironmentLayerRole.Road => "rd_",
                EnvironmentLayerRole.Background => "bg_",
                EnvironmentLayerRole.Background2 => "bg_2_",
                EnvironmentLayerRole.Sky => "sky_",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
            return $"{prefix}{locationSlug}_{daypart}";
        }

        /// <summary>Сохраняет PNG и один спрайт с сохранением Sprite ID при повторном экспорте.</summary>
        private static Sprite SaveSprite(string assetPath, string spriteName, BackgroundTextureData data)
        {
            // Запоминаем Sprite ID до первого переимпорта.
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var existingImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            var spriteId = GUID.Generate();
            if (existingImporter != null)
            {
                var existingProvider = factory.GetSpriteEditorDataProviderFromObject(existingImporter);
                existingProvider?.InitSpriteEditorDataProvider();
                var existingRect = existingProvider?.GetSpriteRects().FirstOrDefault();
                if (existingRect != null)
                    spriteId = existingRect.spriteID;
            }

            // Записываем те же подготовленные пиксели, что использованы в сцене.
            var texture = data.CreateTexture();
            try
            {
                File.WriteAllBytes(GetAbsolutePath(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"TextureImporter не найден: {assetPath}");
            ConfigureImporter(importer, data);
            importer.SaveAndReimport();

            // Длина повтора определяется видимым прямоугольником, а не служебной текстурой.
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
                throw new InvalidOperationException($"Sprite Data Provider недоступен: {assetPath}");
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(new[]
            {
                new SpriteRect
                {
                    name = spriteName,
                    rect = data.SpriteRect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(.5f, .5f),
                    spriteID = spriteId
                }
            });
            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider == null)
                throw new InvalidOperationException($"Sprite Name/File ID Provider недоступен: {assetPath}");
            nameProvider.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteName, spriteId) });
            provider.Apply();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var sprite = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().SingleOrDefault();
            if (sprite == null || sprite.rect != data.SpriteRect ||
                sprite.texture.width != data.Width || sprite.texture.height != data.Height)
                throw new InvalidOperationException($"Импорт изменил размер или не создал точный спрайт: {assetPath}");
            return sprite;
        }

        /// <summary>Настраивает спрайт без изменения размера исходных пикселей.</summary>
        private static void ConfigureImporter(TextureImporter importer, BackgroundTextureData data)
        {
            // Общие настройки рисунка и прямоугольного меша.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = BackgroundTexturePreparation.PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(data.Width, data.Height));
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(textureSettings);

            // Существующие платформенные overrides также сохраняют полный размер.
            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.maxTextureSize = importer.maxTextureSize;
                importer.SetPlatformTextureSettings(platformSettings);
            }
        }

        /// <summary>Назначает ассету адрес в существующей группе окружения.</summary>
        private static void Register(AddressableAssetSettings settings, AddressableAssetGroup group,
            string assetPath, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null)
                throw new InvalidOperationException($"Не удалось зарегистрировать Addressable: {assetPath}");
            entry.address = address;
        }

        /// <summary>Создаёт каталоги через Unity с редакторскими метаданными.</summary>
        private static void EnsureDirectory(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        /// <summary>Переводит путь ассета в абсолютный путь проекта.</summary>
        private static string GetAbsolutePath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
