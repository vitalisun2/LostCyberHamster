using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Assets.Scripts.System
{
    public static class LevelDataProvider
    {
        private static List<AsyncOperationHandle<Sprite>> _introHandles = new List<AsyncOperationHandle<Sprite>>();


        public static async Task LoadLevelData()
        {
            var levelData = LevelController.Instance.LevelData;

            await LoadLevelInfo(levelData);
            await LoadBackgroundPrefab(levelData);
            await LoadBonuses(levelData);
            await LoadEffects(levelData);
            await LoadObstacles(levelData);
            await LoadObstaclesSprites(levelData);
            await LoadDecorSprites(levelData);
            await LoadCollectablesSprites(levelData);
        }

        // Load intro sprites
        /// <summary>
        /// Asynchronously loads intro sprites for the specified level in sequence.
        /// </summary>
        public static async Task LoadIntroSprites()
        {
            _introHandles.Clear();

            List<Sprite> introSprites = new List<Sprite>();
            string levelName = GameDataManager.PlayerData.CurrentLevel;

            int spriteIndex = 1;
            int maxSprites = 10;

            while (spriteIndex <= maxSprites)
            {
                string spriteAddress = $"{levelName}_intro_{spriteIndex}";

                if (!Addressables.ResourceLocators.Any(locator => locator.Locate(spriteAddress, typeof(Sprite), out var _)))
                {
                    break;
                }

                var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);
                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogWarning($"[LoadIntroSprites] Sprite '{spriteAddress}' is null or failed. Stopping here.");
                    break;
                }

                _introHandles.Add(handle);

                introSprites.Add(handle.Result);
                spriteIndex++;
            }

            LevelController.Instance.LevelData.IntroSprites = introSprites;
        }

        public static void ReleaseIntroSprites()
        {
            if (_introHandles.Count == 0)
            {
                Debug.Log("[ReleaseIntroSprites] No intro handles to release.");
                return;
            }

            foreach (var handle in _introHandles)
            {
                Addressables.Release(handle);
            }

            _introHandles.Clear();

            LevelController.Instance.LevelData.IntroSprites.Clear();
        }


        /// <summary>
        /// Asynchronously loads the skip button sprite from Addressables using the address "skip_button".
        /// If the sprite is found, it is assigned to LevelData.SkipButtonSprite.
        /// Logs a warning if the sprite is not found.
        /// </summary>
        public static async Task LoadSkipButtonSprite()
        {
            string skipButtonAddress = "skip_button";

            var handle = Addressables.LoadAssetAsync<Sprite>(skipButtonAddress);

            try
            {
                await handle.Task;

                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    LevelController.Instance.LevelData.SkipButtonSprite = handle.Result;
                }
                else
                {
                    Debug.LogWarning("Skip button sprite not found.");
                }
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private static async Task LoadLevelInfo(LevelData levelData)
        {
            var levelKey = GameDataManager.PlayerData.CurrentLevel;
            var resolvedAddress = ResolveCurrentLevelAddress(levelKey);
            await LoadLevelInfo(levelData, resolvedAddress, levelKey);
        }


        public static Task LoadLevelInfo(LevelData levelData, string levelAddress)
        {
            var fallback = GameDataManager.PlayerData.CurrentLevel;
            return LoadLevelInfo(levelData, levelAddress, fallback);
        }

        private static async Task LoadLevelInfo(LevelData levelData, string levelAddress, string fallbackAddress)
        {
            if (string.IsNullOrWhiteSpace(levelAddress))
            {
                Debug.LogError("[LevelDataProvider] Level address is empty. Aborting load.");
                return;
            }

            var asset = await TryLoadLevelAssetAsync(levelAddress);

            if (asset == null && !string.Equals(levelAddress, fallbackAddress, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[LevelDataProvider] Failed to load level asset '{levelAddress}'. Falling back to legacy address '{fallbackAddress}'.");
                asset = await TryLoadLevelAssetAsync(fallbackAddress);
            }

            if (asset == null)
            {
                Debug.LogError($"[LevelDataProvider] Unable to load level definition for '{fallbackAddress}'.");
                return;
            }

            levelData.LevelInfo = JsonUtility.FromJson<LevelInfo>(asset.text);
        }

        private static async Task<TextAsset> TryLoadLevelAssetAsync(string address)
        {
            try
            {
                return await Addressables.LoadAssetAsync<TextAsset>(address).Task;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelDataProvider] Exception while loading '{address}': {ex.Message}");
                return null;
            }
        }


        private static string ResolveCurrentLevelAddress(string levelKey)
        {
            if (LevelCatalogService.TryFindLevel(levelKey, out var descriptor))
            {
                return descriptor.Address;
            }

            return levelKey;
        }

        private static string ExtractLegacyLevelKey(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return address;
            }

            var fileName = Path.GetFileNameWithoutExtension(address);
            return string.IsNullOrWhiteSpace(fileName) ? address : fileName;
        }

        private static async Task LoadBackgroundPrefab(LevelData levelData)
        {
            var backgroundPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.BackgroundPrefabName).Task;

            var backgroundTexture = await LoadBackgroundSpriteWithFallback(levelData.LevelInfo.backgroundTexture);

            if (backgroundTexture == null)
            {
                Debug.LogError("[LevelDataProvider] Unable to load background sprite for the current level.");
                return;
            }

            LevelDataValidator.ValidateBackgroundTexture(backgroundTexture);

            var backgroundRenderer = backgroundPrefab.GetComponentInChildren<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundTexture;

            levelData.BackgroundPrefab = backgroundPrefab;
        }

        private static async Task LoadBonuses(LevelData levelData)
        {
            var coinOneBonusPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.CoinOneBonusPrefabName).Task;
            levelData.CoinOneBonusPrefab = coinOneBonusPrefab.GetComponent<CoinOneBonus>();

            var crystalBonusPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.CrystalBonusPrefabName).Task;
            levelData.CrystalBonusPrefab = crystalBonusPrefab.GetComponent<CrystalBonus>();

            var energeticBonusPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.EnergeticBonusPrefabName).Task;
            levelData.EnergeticBonusPrefab = energeticBonusPrefab.GetComponent<EnergeticBonus>();

            var lifeBonusPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.LifeBonusPrefabName).Task;
            levelData.LifeBonusPrefab = lifeBonusPrefab.GetComponent<LifeBonus>();

            var pizzaBonusPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.PizzaBonusPrefabName).Task;
            levelData.PizzaBonusPrefab = pizzaBonusPrefab.GetComponent<PizzaBonus>();
        }

        //load effects
        private static async Task LoadEffects(LevelData levelData)
        {
            var boomEffectPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.BoomEffectPrefabName).Task;
            levelData.BoomEffectPrefab = boomEffectPrefab.GetComponent<BoomEffect>();
        }

        // Load obstacles
        private static async Task LoadObstacles(LevelData levelData)
        {
            var smallCitizenPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.SmallCitizenPrefabName).Task;
            levelData.SmallCitizenPrefab = smallCitizenPrefab;

            var bigCitizenPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.BigCitizenPrefabName).Task;
            levelData.BigCitizenPrefab = bigCitizenPrefab;

            var bigNotAlivePrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.BigNotAlivePrefabName).Task;
            levelData.BigNotAlivePrefab = bigNotAlivePrefab;
        }

        // Load obstacles sprites
        private static async Task LoadObstaclesSprites(LevelData levelData)
        {
            var primaryLabel = GetObstaclesSpritesLabel();
            var fallbackLabel = GetFallbackObstaclesLabel();

            var sprites = await LoadSpritesByLabelAsync(primaryLabel, "obstacle sprites", fallbackLabel);

            levelData.ObstaclesSprites = sprites;

            if (!sprites.Any())
            {
                Debug.LogWarning("[LevelDataProvider] No obstacle sprites found for the configured labels.");
            }
        }
        private static string GetObstaclesSpritesLabel()
        {
            var locationName = LevelManager.GetLocationName();
            return BuildLocationLabel(locationName, Consts.ObstaclesSpritesLabelPostfix);
        }

        private static string GetFallbackObstaclesLabel()
        {
            return BuildLocationLabel(GetFallbackLocationName(), Consts.ObstaclesSpritesLabelPostfix);
        }
        // load collectable sprites
        private static async Task LoadCollectablesSprites(LevelData levelData)
        {
            levelData.CollectablesSprites = await LoadSpritesByLabelAsync(Consts.CollectableSpritesLabel, "collectable sprites");
            LevelDataValidator.ValidateCollectableSprites(levelData.CollectablesSprites);
        }
        //load decor sprites
        private static async Task LoadDecorSprites(LevelData levelData)
        {
            var primaryLabel = GetDecorSpritesLabel();
            var fallbackLabel = GetFallbackDecorLabel();

            var decorSprites = await LoadSpritesByLabelAsync(primaryLabel, "decor sprites", fallbackLabel);

            levelData.DecorSprites = decorSprites;
            LevelDataValidator.ValidateDecorSprites(levelData.DecorSprites);
        }


        // get obstacles sprites label by current level and postfix
        // get decor sprites label by current level and postfix
        private static string GetDecorSpritesLabel()
        {
            var locationName = LevelManager.GetLocationName();
            return BuildLocationLabel(locationName, Consts.DecorSpritesLabelPostFix);
        }

        private static string GetFallbackDecorLabel()
        {
            return BuildLocationLabel(GetFallbackLocationName(), Consts.DecorSpritesLabelPostFix);
        }

        private static string BuildDecorLabel(string locationName)
        {
            return BuildLocationLabel(locationName, Consts.DecorSpritesLabelPostFix);
        }

        private static string BuildLocationLabel(string locationName, string postfix)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                locationName = GetFallbackLocationName();
            }

            return $"{locationName} {postfix}";
        }

        private static string GetFallbackLocationName()
        {
            var locationInfos = LevelManager.LocationInfoList?.locations;
            if (locationInfos != null)
            {
                foreach (var info in locationInfos)
                {
                    if (!string.IsNullOrWhiteSpace(info?.name))
                    {
                        return info.name;
                    }
                }
            }

            return "New York";
        }
        private static async Task<List<Sprite>> LoadSpritesByLabelAsync(string label, string description, params string[] additionalFallbackLabels)
        {
            var candidates = new List<(string Label, bool IsFallback, string Reason)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string candidateLabel, bool isFallback, string reason)
            {
                if (string.IsNullOrWhiteSpace(candidateLabel))
                {
                    return;
                }

                var trimmed = candidateLabel.Trim();
                if (seen.Add(trimmed))
                {
                    candidates.Add((trimmed, isFallback, reason));
                }
            }

            AddCandidate(label, false, "primary");

            if (additionalFallbackLabels is { Length: > 0 })
            {
                foreach (var fallbackLabel in additionalFallbackLabels)
                {
                    AddCandidate(fallbackLabel, true, "explicit");
                }
            }

            var currentLocation = TryGetCurrentLocationName();
            var fallbackLocation = GetFallbackLocationName();

            if (!string.IsNullOrWhiteSpace(label) &&
                !string.IsNullOrWhiteSpace(currentLocation) &&
                !string.IsNullOrWhiteSpace(fallbackLocation) &&
                !string.Equals(currentLocation, fallbackLocation, StringComparison.OrdinalIgnoreCase))
            {
                var locationFallback = LocationAssetFallback.TryBuildFallbackLabel(label, currentLocation, fallbackLocation);
                AddCandidate(locationFallback, true, "location");
            }

            foreach (var (candidateLabel, isFallback, reason) in candidates)
            {
                var sprites = await TryLoadSpritesForLabel(candidateLabel, description);
                if (sprites.Count > 0)
                {
                    if (isFallback)
                    {
                        var locationInfo = currentLocation ?? "unknown";
                        Debug.LogWarning($"[LevelDataProvider] {description} for location '{locationInfo}' not found. Using fallback label '{candidateLabel}' ({reason}).");
                    }

                    return sprites;
                }
            }

            if (candidates.Count > 1)
            {
                Debug.LogWarning($"[LevelDataProvider] Unable to load {description}. Tried labels: {string.Join(", ", candidates.Select(c => c.Label))}.");
            }

            return new List<Sprite>();
        }

        private static async Task<List<Sprite>> TryLoadSpritesForLabel(string label, string description)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return new List<Sprite>();
            }

            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = default;
            try
            {
                locationsHandle = Addressables.LoadResourceLocationsAsync(label, typeof(Sprite));
                var locations = await locationsHandle.Task;

                if (locations == null || locations.Count == 0)
                {
                    Debug.LogWarning($"[LevelDataProvider] Addressables label '{label}' has no sprite entries ({description}).");
                    return new List<Sprite>();
                }

                var loadHandle = Addressables.LoadAssetsAsync<Sprite>(locations, null);
                try
                {
                    var sprites = await loadHandle.Task;
                    return sprites?.ToList() ?? new List<Sprite>();
                }
                finally
                {
                    if (loadHandle.IsValid())
                    {
                        Addressables.Release(loadHandle);
                    }
                }
            }
            catch (InvalidKeyException)
            {
                Debug.LogWarning($"[LevelDataProvider] Addressables label '{label}' not found for {description}.");
                return new List<Sprite>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelDataProvider] Failed to load sprites for label '{label}' ({description}): {ex.Message}");
                return new List<Sprite>();
            }
            finally
            {
                if (locationsHandle.IsValid())
                {
                    Addressables.Release(locationsHandle);
                }
            }
        }

        private static string TryGetCurrentLocationName()
        {
            try
            {
                return LevelManager.GetLocationName();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelDataProvider] Failed to resolve current location name: {ex.Message}");
                return null;
            }
        }

        private static async Task<Sprite> LoadBackgroundSpriteWithFallback(string primaryKey)
        {
            var sprite = await TryLoadSpriteByKey(primaryKey, "background sprite");
            if (sprite != null)
            {
                return sprite;
            }

            var fallbackLocationName = GetFallbackLocationName();
            var partOfDay = LevelManager.GetCurrentPartOfDay();
            var fallbackKey = LocationAssetFallback.TryBuildFallbackBackgroundKey(primaryKey, fallbackLocationName, partOfDay);

            if (!string.IsNullOrWhiteSpace(fallbackKey) && !string.Equals(fallbackKey, primaryKey, StringComparison.OrdinalIgnoreCase))
            {
                var fallbackSprite = await TryLoadSpriteByKey(fallbackKey, "background sprite fallback");
                if (fallbackSprite != null)
                {
                    Debug.LogWarning($"[LevelDataProvider] Background sprite '{primaryKey}' not found. Using fallback '{fallbackKey}'.");
                    return fallbackSprite;
                }
            }

            return sprite;
        }

        private static async Task<Sprite> TryLoadSpriteByKey(string key, string description)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            try
            {
                return await Addressables.LoadAssetAsync<Sprite>(key).Task;
            }
            catch (InvalidKeyException)
            {
                Debug.LogWarning($"[LevelDataProvider] Addressables key '{key}' not found for {description}.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelDataProvider] Failed to load sprite '{key}' ({description}): {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Возвращает список имён (без расширения) всех JSON-файлов с меткой "Levels".
        /// </summary>
        public static Task<List<string>> GetAllLevelNamesAsync()
        {
            return GetAllLevelNamesAsync(LevelCatalogService.HasCatalog);
        }

        public static async Task<List<string>> GetAllLevelNamesAsync(bool preferHierarchical)
        {
            if (preferHierarchical)
            {
                var hierarchical = await GetHierarchicalLevelNamesAsync();
                if (hierarchical.Count > 0)
                {
                    return hierarchical;
                }
            }

            return await GetLegacyLevelNamesAsync();
        }

        private static async Task<List<string>> GetLegacyLevelNamesAsync()
        {
            var handle = Addressables.LoadResourceLocationsAsync(Consts.Levels, typeof(TextAsset));
            var locations = await handle.Task;

            try
            {
                if (locations == null || locations.Count == 0)
                {
                    Debug.LogWarning("Не найдено ни одного JSON-файла с меткой \"Levels\". Проверьте настройки Addressables.");
                    return new List<string>();
                }

                var levelNames = locations
                    .Select(location => Path.GetFileNameWithoutExtension(location.InternalId))
                    .ToList();

                await ValidateDayPartGroupingAsync(levelNames);

                return levelNames;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private static async Task<List<string>> GetHierarchicalLevelNamesAsync()
        {
            var handle = Addressables.LoadResourceLocationsAsync(Consts.LevelsDaypart, typeof(TextAsset));

            try
            {
                var locations = await handle.Task;
                if (locations == null || locations.Count == 0)
                {
                    return new List<string>();
                }

                var comparer = StringComparer.OrdinalIgnoreCase;
                var names = new HashSet<string>(comparer);

                foreach (var location in locations)
                {
                    var address = location?.PrimaryKey;
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        continue;
                    }

                    names.Add(ExtractLegacyLevelKey(address));
                }

                var result = names.ToList();
                result.Sort(StringComparer.OrdinalIgnoreCase);
                return result;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private static async Task ValidateDayPartGroupingAsync(ICollection<string> legacyLevelNames)
        {
            var legacySet = new HashSet<string>(legacyLevelNames);
            var partNames = Enum.GetNames(typeof(PartOfDayEnum));
            var partSet = new HashSet<string>(partNames, StringComparer.OrdinalIgnoreCase);
            var locationsByPart = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

            AsyncOperationHandle<IList<IResourceLocation>>? handle = null;

            try
            {
                handle = Addressables.LoadResourceLocationsAsync(Consts.LevelsDaypart, typeof(TextAsset));
                var dayPartLocations = await handle.Value.Task;

                if (dayPartLocations == null || dayPartLocations.Count == 0)
                {
                    Debug.LogWarning("[LevelDataProvider] Label '" + Consts.LevelsDaypart + "' has no entries. Day-part catalog is not configured yet.");
                    return;
                }

                foreach (var location in dayPartLocations)
                {
                    var address = location?.PrimaryKey;
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        Debug.LogWarning("[LevelDataProvider] Encountered day-part entry with empty address.");
                        continue;
                    }

                    var segments = address.Split('/');
                    if (segments.Length != 3)
                    {
                        Debug.LogWarning($"[LevelDataProvider] Unexpected day-part address format '{address}'. Expected '<Location>/<Part>/<level_XX>'.");
                        continue;
                    }

                    var locationKey = segments[0];
                    var partKey = segments[1];
                    var levelKey = segments[2];

                    if (!partSet.Contains(partKey))
                    {
                        Debug.LogWarning($"[LevelDataProvider] Address '{address}' uses unknown part-of-day '{partKey}'.");
                        continue;
                    }

                    if (!legacySet.Contains(levelKey))
                    {
                        Debug.LogWarning($"[LevelDataProvider] Address '{address}' points to '{levelKey}' which is not present in legacy label '{Consts.Levels}'.");
                    }

                    if (!locationsByPart.TryGetValue(locationKey, out var parts))
                    {
                        parts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                        locationsByPart[locationKey] = parts;
                    }

                    if (!parts.TryGetValue(partKey, out var levels))
                    {
                        levels = new List<string>();
                        parts[partKey] = levels;
                    }

                    if (!levels.Contains(levelKey))
                    {
                        levels.Add(levelKey);
                    }
                    else
                    {
                        Debug.LogWarning($"[LevelDataProvider] Duplicate day-part entry for level '{levelKey}' in '{address}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelDataProvider] Failed to validate day-part level mapping: {ex.Message}");
            }
            finally
            {
                if (handle.HasValue)
                {
                    Addressables.Release(handle.Value);
                }
            }

            foreach (var (location, parts) in locationsByPart)
            {
                foreach (var part in partNames)
                {
                    if (!parts.TryGetValue(part, out var levels) || levels.Count == 0)
                    {
                        Debug.LogWarning($"[LevelDataProvider] Location '{location}' has no levels registered for part '{part}' in label '{Consts.LevelsDaypart}'.");
                    }
                }
            }

            var dayPartLevels = new HashSet<string>(locationsByPart.Values.SelectMany(p => p.Values).SelectMany(x => x));
            var missing = legacySet.Except(dayPartLevels).ToList();
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[LevelDataProvider] The following legacy levels are missing from '{Consts.LevelsDaypart}': {string.Join(", ", missing)}");
            }
        }

    }
}













