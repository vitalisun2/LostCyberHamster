using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Assets.Scripts.System.LevelManagement;
using Assets.Scripts.System.Resources;

namespace Assets.Scripts.System
{
    public static class LevelDataProvider
    {
        private static List<AsyncOperationHandle<Sprite>> _introHandles = new List<AsyncOperationHandle<Sprite>>();


        public static async Task LoadLevelData()
        {
            var levelData = LevelController.Instance.LevelData;

            await LoadLevelInfo(levelData);
            await LoadSkyPrefab(levelData);
            await LoadBackground2Prefab(levelData);
            await LoadBackgroundPrefab(levelData);
            await LoadRoadPrefab(levelData);
            await LoadBonuses(levelData);
            await LoadEffects(levelData);
            await LoadObstacles(levelData);
            await LoadObstaclesSprites(levelData);
            await LoadObstacleAnimations(levelData);
            await LoadDecorSprites(levelData);
            await LoadCollectablesSprites(levelData);
        }

        // Load intro sprites
        /// <summary>
        /// Asynchronously loads intro sprites for the specified level in sequence.
        /// </summary>
        public static async Task<bool> LoadIntroSprites()
        {
            _introHandles.Clear();

            var introSprites = new List<Sprite>();
            var levelIdentifier = GameDataManager.PlayerData.CurrentLevel;
            var baseAddress = ResolveIntroBaseAddress(levelIdentifier);

            if (string.IsNullOrWhiteSpace(baseAddress))
            {
                Debug.LogWarning("[LoadIntroSprites] Unable to resolve intro base address. Skip loading.");
                LevelController.Instance.LevelData.IntroSprites = introSprites;
                return false;
            }

            const int maxSprites = 10;
            for (int spriteIndex = 1; spriteIndex <= maxSprites; spriteIndex++)
            {
                var spriteAddress = $"{baseAddress}/intro_{spriteIndex:00}";

                if (!CanLocateSprite(spriteAddress))
                {
                    if (spriteIndex == 1)
                    {
                        Debug.LogWarning($"[LoadIntroSprites] Address '{spriteAddress}' not found via resource locators.");
                    }
                    break;
                }

                var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);
                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogWarning($"[LoadIntroSprites] Sprite '{spriteAddress}' failed with status {handle.Status}. Breaking.");
                    Addressables.Release(handle);
                    break;
                }

                _introHandles.Add(handle);
                introSprites.Add(handle.Result);
            }

            var hasIntroSprites = introSprites.Count > 0;

            if (!hasIntroSprites)
            {
                Debug.LogWarning($"[LoadIntroSprites] No intro sprites loaded for '{baseAddress}'.");
            }

            LevelController.Instance.LevelData.IntroSprites = introSprites;
            return hasIntroSprites;
        }

        private static string ResolveIntroBaseAddress(string levelIdentifier)
        {
            if (LevelCatalogService.TryFindLevel(levelIdentifier, out var descriptor) && !string.IsNullOrWhiteSpace(descriptor.Address))
            {
                return descriptor.Address.Trim();
            }

            return string.IsNullOrWhiteSpace(levelIdentifier) ? string.Empty : levelIdentifier.Trim();
        }

        private static bool CanLocateSprite(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator != null && locator.Locate(address, typeof(Sprite), out var _))
                {
                    return true;
                }
            }

            return false;
        }


public static void ReleaseIntroSprites()
        {
            if (_introHandles.Count == 0)
            {
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
                Debug.LogWarning($"[LevelDataProvider] Failed to load level asset '{levelAddress}'. Falling back to '{fallbackAddress}'.");
                asset = await TryLoadLevelAssetAsync(fallbackAddress);
            }

            if (asset == null)
            {
                Debug.LogError($"[LevelDataProvider] Unable to load level definition for '{fallbackAddress}'.");
                return;
            }

            var levelRef = JsonUtility.FromJson<LevelInfoRef>(asset.text);

            var patternsAsset = await LoadPatternsCollectionAsync();
            var patterns = JsonUtility.FromJson<PatternsCollection>(patternsAsset.text);

            var theme = await LoadLocationThemeWithFallbackAsync(levelRef.location);

            levelData.LevelInfo = LevelResolver.Resolve(levelRef, patterns, theme);
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

        private static async Task<TextAsset> LoadPatternsCollectionAsync()
        {
            return await Addressables.LoadAssetAsync<TextAsset>("PatternsCollection").Task;
        }

        private static async Task<LocationTheme> LoadLocationThemeWithFallbackAsync(string location)
        {
            var themeAsset = await TryLoadLocationThemeAssetAsync(location);
            var theme = themeAsset != null
                ? JsonUtility.FromJson<LocationTheme>(themeAsset.text)
                : new LocationTheme();

            if (string.Equals(location, Consts.TemplatesFallbackLocation, StringComparison.OrdinalIgnoreCase))
            {
                return theme;
            }

            var fallbackAsset = await TryLoadLocationThemeAssetAsync(Consts.TemplatesFallbackLocation);
            var fallbackTheme = fallbackAsset != null
                ? JsonUtility.FromJson<LocationTheme>(fallbackAsset.text)
                : new LocationTheme();

            return LocationAssetFallback.MergeLocationTheme(theme, fallbackTheme);
        }

        private static async Task<TextAsset> TryLoadLocationThemeAssetAsync(string location)
        {
            try
            {
                return await LoadLocationThemeAssetAsync(location);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelDataProvider] Failed to load location theme for '{location}': {ex.Message}");
                return null;
            }
        }

        private static async Task<TextAsset> LoadLocationThemeAssetAsync(string location)
        {
            var address = $"{location}/obstacle_sprite_to_type_mappings";
            return await Addressables.LoadAssetAsync<TextAsset>(address).Task;
        }


        private static string ResolveCurrentLevelAddress(string levelKey)
        {
            if (LevelCatalogService.TryFindLevel(levelKey, out var descriptor))
            {
                return descriptor.Address;
            }

            return levelKey;
        }

        private static async Task LoadBackgroundPrefab(LevelData levelData)
        {
            // Load shared prefab only once if not already loaded
            if (levelData.ScrollingEnvironmentPrefab == null)
            {
                levelData.ScrollingEnvironmentPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.ScrollingEnvironmentPrefabName).Task;
            }

            var backgroundKey = EnvironmentKeyResolver.BuildBackgroundKey();
            var backgroundSprite = await TryLoadEnvironmentSprite(backgroundKey, "background");

            if (backgroundSprite == null)
            {
                Debug.LogError("[LevelDataProvider] Unable to load background sprite for the current level.");
                return;
            }

            LevelDataValidator.ValidateBackgroundTexture(backgroundSprite);
            levelData.BackgroundSprite = backgroundSprite;
        }

        private static async Task LoadBackground2Prefab(LevelData levelData)
        {
            // Load shared prefab only once if not already loaded
            if (levelData.ScrollingEnvironmentPrefab == null)
            {
                levelData.ScrollingEnvironmentPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.ScrollingEnvironmentPrefabName).Task;
            }

            var background2Key = EnvironmentKeyResolver.BuildBackground2Key();
            var background2Sprite = await TryLoadEnvironmentSprite(background2Key, "background2");

            if (background2Sprite == null)
            {
                Debug.LogWarning("[LevelDataProvider] Unable to load background2 sprite, continuing without it.");
                return;
            }

            LevelDataValidator.ValidateBackgroundTexture(background2Sprite);
            levelData.Background2Sprite = background2Sprite;
        }

        private static async Task LoadRoadPrefab(LevelData levelData)
        {
            // Load shared prefab only once if not already loaded
            if (levelData.ScrollingEnvironmentPrefab == null)
            {
                levelData.ScrollingEnvironmentPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.ScrollingEnvironmentPrefabName).Task;
            }

            var roadKey = EnvironmentKeyResolver.BuildRoadKey();
            var roadSprite = await TryLoadEnvironmentSprite(roadKey, "road");

            if (roadSprite == null)
            {
                Debug.LogError("[LevelDataProvider] Unable to load road sprite for the current level.");
                return;
            }

            levelData.RoadSprite = roadSprite;
        }

        private static async Task LoadSkyPrefab(LevelData levelData)
        {
            // Load shared prefab only once if not already loaded
            if (levelData.ScrollingEnvironmentPrefab == null)
            {
                levelData.ScrollingEnvironmentPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.ScrollingEnvironmentPrefabName).Task;
            }

            var skyKey = EnvironmentKeyResolver.BuildSkyKey();
            var skySprite = await TryLoadEnvironmentSprite(skyKey, "sky");

            if (skySprite == null)
            {
                Debug.LogError("[LevelDataProvider] Unable to load sky sprite for the current level.");
                return;
            }

            levelData.SkySprite = skySprite;
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

            var mediumOrBigNotAlivePrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.MediumOrBigNotAlivePrefabName).Task;
            levelData.MediumOrBigNotAlivePrefab = mediumOrBigNotAlivePrefab;
        }

        // Load obstacles sprites
        private static async Task LoadObstaclesSprites(LevelData levelData)
        {
            var primaryLabel = GetObstaclesSpritesLabel();
            var fallbackLabel = GetFallbackObstaclesLabel();

            levelData.ObstaclesSpritesLease?.Dispose();
            var obstaclesLease = await LoadSpritesByLabelAsync(primaryLabel, "obstacle sprites", fallbackLabel);
            levelData.ObstaclesSpritesLease = obstaclesLease;
            levelData.ObstaclesSprites = obstaclesLease != null
                ? new List<Sprite>(obstaclesLease.Values)
                : new List<Sprite>();

            if (!levelData.ObstaclesSprites.Any())
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

        // Load obstacle animation clips
        private static async Task LoadObstacleAnimations(LevelData levelData)
        {
            var primaryLabel = GetObstacleAnimationsLabel();
            var fallbackLabel = GetFallbackObstacleAnimationsLabel();

            levelData.ObstacleAnimationClipsLease?.Dispose();
            var animationsLease = await LoadAnimationClipsByLabelAsync(primaryLabel, "obstacle animations", fallbackLabel);
            levelData.ObstacleAnimationClipsLease = animationsLease;
            levelData.ObstacleAnimationClips = animationsLease != null
                ? new List<AnimationClip>(animationsLease.Values)
                : new List<AnimationClip>();
        }

        private static string GetObstacleAnimationsLabel()
        {
            var locationName = LevelManager.GetLocationName();
            return BuildLocationLabel(locationName, Consts.ObstacleAnimationsLabelPostfix);
        }

        private static string GetFallbackObstacleAnimationsLabel()
        {
            return BuildLocationLabel(GetFallbackLocationName(), Consts.ObstacleAnimationsLabelPostfix);
        }

        private static async Task<AddressableSetLease<AnimationClip>> LoadAnimationClipsByLabelAsync(string label, string description, params string[] additionalFallbackLabels)
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

            foreach (var (candidateLabel, isFallback, reason) in candidates)
            {
                if (!CanLocateAssets(candidateLabel, typeof(AnimationClip)))
                {
                    continue;
                }

                AddressableSetLease<AnimationClip> lease = null;
                try
                {
                    lease = await AddressableLoader.LoadAssetsByLabelAsync<AnimationClip>(candidateLabel);
                }
                catch (InvalidKeyException)
                {
               }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelDataProvider] Failed to load animation clips for label '{candidateLabel}' ({description}): {ex.Message}");
                }

                if (lease == null)
                {
                    continue;
                }

                if (lease.Values.Count > 0)
                {
                    if (isFallback)
                    {
                        var locationInfo = TryGetCurrentLocationName() ?? "unknown";
                        Debug.LogWarning($"[LevelDataProvider] {description} for location '{locationInfo}' not found. Using fallback label '{candidateLabel}' ({reason}).");
                    }

                    return lease;
                }

                lease.Dispose();
            }

            return null;
        }
        // load collectable sprites
        private static async Task LoadCollectablesSprites(LevelData levelData)
        {
            levelData.CollectablesSpritesLease?.Dispose();
            var collectablesLease = await LoadSpritesByLabelAsync(Consts.CollectableSpritesLabel, "collectable sprites");
            levelData.CollectablesSpritesLease = collectablesLease;
            levelData.CollectablesSprites = collectablesLease != null
                ? new List<Sprite>(collectablesLease.Values)
                : new List<Sprite>();
            LevelDataValidator.ValidateCollectableSprites(levelData.CollectablesSprites);
        }
        //load decor sprites
        private static async Task LoadDecorSprites(LevelData levelData)
        {
            var primaryLabel = GetDecorSpritesLabel();
            var fallbackLabel = GetFallbackDecorLabel();

            levelData.DecorSpritesLease?.Dispose();
            var decorLease = await LoadSpritesByLabelAsync(primaryLabel, "decor sprites", fallbackLabel);
            levelData.DecorSpritesLease = decorLease;
            levelData.DecorSprites = decorLease != null
                ? new List<Sprite>(decorLease.Values)
                : new List<Sprite>();
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
        private static async Task<AddressableSetLease<Sprite>> LoadSpritesByLabelAsync(string label, string description, params string[] additionalFallbackLabels)
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
                if (!CanLocateAssets(candidateLabel, typeof(Sprite)))
                {
                    continue;
                }

                AddressableSetLease<Sprite> lease = null;
                try
                {
                    lease = await AddressableLoader.LoadAssetsByLabelAsync<Sprite>(candidateLabel);
                }
                catch (InvalidKeyException)
                {
                    Debug.LogWarning($"[LevelDataProvider] Addressables label '{candidateLabel}' not found for {description}.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelDataProvider] Failed to load sprites for label '{candidateLabel}' ({description}): {ex.Message}");
                }

                if (lease == null)
                {
                    continue;
                }

                if (lease.Values.Count > 0)
                {
                    if (isFallback)
                    {
                        var locationInfo = currentLocation ?? "unknown";
                        Debug.LogWarning($"[LevelDataProvider] {description} for location '{locationInfo}' not found. Using fallback label '{candidateLabel}' ({reason}).");
                    }

                    return lease;
                }

                lease.Dispose();
            }

            if (candidates.Count > 1)
            {
                Debug.LogWarning($"[LevelDataProvider] Unable to load {description}. Tried labels: {string.Join(", ", candidates.Select(c => c.Label))}.");
            }

            return null;
        }

        /// <summary>
        /// Проверяет, есть ли в Addressables ассеты нужного типа для label, не создавая error-handle.
        /// </summary>
        private static bool CanLocateAssets(string label, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator != null
                    && locator.Locate(label, assetType, out var locations)
                    && locations != null
                    && locations.Count > 0)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Loads an environment sprite by its resolved key. Used for background, background2, road, sky.
        /// </summary>
        private static async Task<Sprite> TryLoadEnvironmentSprite(string key, string assetType)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError($"[LevelDataProvider] Unable to build {assetType} texture key.");
                return null;
            }

            var sprite = await TryLoadSpriteByKey(key, $"{assetType} sprite");
            if (sprite == null)
            {
                Debug.LogWarning($"[LevelDataProvider] {assetType} sprite '{key}' not found.");
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
        /// Возвращает список полных адресов gameplay-уровней, сохраняя legacy-имя метода.
        /// </summary>
        public static Task<List<string>> GetAllLevelNamesAsync()
        {
            return GetAllLevelAddressesAsync();
        }

        /// <summary>
        /// Возвращает список полных адресов gameplay-уровней, известных иерархическому каталогу.
        /// </summary>
        public static async Task<List<string>> GetAllLevelAddressesAsync()
        {
            if (LevelCatalogService.HasCatalog && !LevelCatalogService.Catalog.IsEmpty)
            {
                var fromCatalog = LevelCatalogService.Catalog
                    .EnumerateLevels()
                    .Select(level => level.Address?.Trim())
                    .Where(address => !string.IsNullOrWhiteSpace(address))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (fromCatalog.Count > 0)
                {
                    return fromCatalog;
                }
            }

            return await GetHierarchicalLevelAddressesAsync();
        }

        private static async Task<List<string>> GetHierarchicalLevelAddressesAsync()
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
                var addresses = new HashSet<string>(comparer);

                foreach (var location in locations)
                {
                    var address = location?.PrimaryKey?.Trim();
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        continue;
                    }

                    if (!HierarchicalLevelCatalog.IsGameplayLevelAddress(address))
                    {
                        continue;
                    }

                    addresses.Add(address);
                }

                var result = addresses.ToList();
                result.Sort(StringComparer.OrdinalIgnoreCase);
                return result;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

    }
}











