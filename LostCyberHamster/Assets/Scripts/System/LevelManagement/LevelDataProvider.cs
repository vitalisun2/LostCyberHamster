using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
            var levelKey = LevelManager.GetCurrentKey();

            int spriteIndex = 1;
            int maxSprites = 10;

            while (spriteIndex <= maxSprites)
            {
                string spriteAddress = LevelPathBuilder.IntroImage(levelKey, spriteIndex);

                if (!Addressables.ResourceLocators.Any(locator => locator.Locate(spriteAddress, typeof(Sprite), out var _)))
                {
                    break;
                }

                var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);
                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogWarning($"[LoadIntroSprites] Sprite '{spriteAddress}' is null or failed. Stopping here.");
                    Addressables.Release(handle);
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
            var key = LevelManager.GetCurrentKey();
            var handle = Addressables.LoadAssetAsync<TextAsset>(LevelPathBuilder.Build(key));
            var asset = await handle.Task;
            levelData.LevelInfo = JsonUtility.FromJson<LevelInfo>(asset.text);
            Addressables.Release(handle);
        }

        private static async Task LoadBackgroundPrefab(LevelData levelData)
        {
            var backgroundPrefab = await Addressables.LoadAssetAsync<GameObject>(Consts.BackgroundPrefabName).Task;
            var backgroundTexture = await Addressables.LoadAssetAsync<Sprite>(levelData.LevelInfo.backgroundTexture).Task;

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
            levelData.ObstaclesSprites = (await Addressables.LoadAssetsAsync<Sprite>(GetObstaclesSpritesLabel(), null).Task).ToList();

            if (!levelData.ObstaclesSprites.Any())
                Debug.LogError($"No obstacle sprites found for location {LevelManager.GetLocationName()}");
        }

        // load collectable sprites
        private static async Task LoadCollectablesSprites(LevelData levelData)
        {
            levelData.CollectablesSprites = (await Addressables.LoadAssetsAsync<Sprite>(Consts.CollectableSpritesLabel, null).Task).ToList();

            LevelDataValidator.ValidateCollectableSprites(levelData.CollectablesSprites);
        }

        //load decor sprites
        private static async Task LoadDecorSprites(LevelData levelData)
        {
            levelData.DecorSprites = (await Addressables.LoadAssetsAsync<Sprite>(GetDecorSpritesLabel(), null).Task).ToList();
            LevelDataValidator.ValidateDecorSprites(levelData.DecorSprites);
        }


        // get obstacles sprites label by current level and postfix
        private static string GetObstaclesSpritesLabel()
        {
            return $"{LevelManager.GetLocationName()} {Consts.ObstaclesSpritesLabelPostfix}";
        }

        // get decor sprites label by current level and postfix
        private static string GetDecorSpritesLabel()
        {
            return $"{LevelManager.GetLocationName()} {Consts.DecorSpritesLabelPostFix}";
        }

        /// <summary>
        /// Возвращает список имён (без расширения) всех JSON-файлов с меткой "Levels".
        /// </summary>
        public static async Task<List<string>> GetAllLevelNamesAsync()
        {
            // Запрашиваем локации у Addressables по метке "Levels" для объектов типа TextAsset.
            var handle = Addressables.LoadResourceLocationsAsync(Consts.Levels, typeof(TextAsset));
            var locations = await handle.Task;

            if (locations == null || locations.Count == 0)
            {
                Debug.LogWarning("Не найдено ни одного JSON-файла с меткой \"Levels\". Проверьте настройки Addressables.");
                return new List<string>();
            }

            // Для каждого ресурса получаем имя файла без расширения (обычно InternalId хранит путь к файлу)
            var levelNames = locations
                .Select(location => Path.GetFileNameWithoutExtension(location.InternalId))
                .ToList();

            // Освобождаем handle
            Addressables.Release(handle);

            return levelNames;
        }
    }

}
