using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using GameManagement;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.AddressableAssets;
using LocationInfoModel = Assets.Scripts.Common.Models.LocationInfo;

namespace Assets.Scripts.System
{
    public static class LevelManager
    {
        private const int _starUnlockOffset = 2;

        public static LocationInfoList LocationInfoList { get; private set; } = new();

        public static List<LocationInfoModel> OpenedLocations => BuildOpenedLocations();

        public static int StarsToOpenNewLocation => CalculateStarsToOpenNextLocation();

        private static HierarchicalLevelCatalog Catalog => LevelCatalogService.Catalog;

        private static bool HasCatalog => LevelCatalogService.HasCatalog;

        private static LevelProgressSnapshot Progress => GameDataManager.PlayerData?.Progress ?? LevelProgressSnapshot.Empty;

        public static async Task Init()
        {
            await InitLocationsList();
        }

        public static async Task LoadLevelData()
        {
            await LevelDataProvider.LoadLevelData();
        }

        public static int GetCurrentLevelNumber()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (string.IsNullOrWhiteSpace(currentLevel))
            {
                return 0;
            }

            if (!TryFindDescriptor(currentLevel, out var descriptor))
            {
                return 0;
            }

            var index = GetSequentialIndex(descriptor);
            return index >= 0 ? index + 1 : 0;
        }

        public static string GetLevelName(int locationIndex, string partOfDayKey)
        {
            var levels = GetLevelsForPartOfDay(locationIndex, partOfDayKey)?.ToList();
            if (levels == null || levels.Count == 0)
            {
                return string.Empty;
            }

            return levels[0];
        }

        public static bool TryParseLevelNumber(string levelKey, out int levelNumber)
        {
            levelNumber = 0;

            if (!TryFindDescriptor(levelKey, out var descriptor))
            {
                return false;
            }

            var index = GetSequentialIndex(descriptor);
            if (index < 0)
            {
                return false;
            }

            levelNumber = index + 1;
            return true;
        }

        public static bool TryResolveLevelKey(string levelKey, out int locationIndex, out string partOfDayKey, out int levelOrder)
        {
            locationIndex = -1;
            partOfDayKey = string.Empty;
            levelOrder = -1;

            if (!TryFindDescriptor(levelKey, out var descriptor))
            {
                return false;
            }

            locationIndex = descriptor.LocationIndex;
            partOfDayKey = descriptor.PartId;
            levelOrder = descriptor.LevelIndex;
            return true;
        }

        public static bool TryGetNextLevelKey(string currentLevelKey, out string nextLevelKey)
        {
            nextLevelKey = string.Empty;

            if (!TryFindDescriptor(currentLevelKey, out var descriptor))
            {
                return false;
            }

            var descriptors = EnumerateDescriptors();
            var index = descriptors.FindIndex(d =>
                string.Equals(d.LevelKey, descriptor.LevelKey, StringComparison.OrdinalIgnoreCase));

            if (index < 0 || index + 1 >= descriptors.Count)
            {
                return false;
            }

            nextLevelKey = descriptors[index + 1].LevelKey;
            return true;
        }

        public static int GetTotalLevelsCount()
        {
            return HasCatalog ? Catalog.EnumerateLevels().Count() : 0;
        }

        public static IEnumerable<string> GetPartOfDayKeys(int locationIndex)
        {
            if (!HasCatalog || !Catalog.TryGetLocation(locationIndex, out var location))
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            var parts = location.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();

            for (int index = 0; index < parts.Count; index++)
            {
                var part = parts[index];
                var key = !string.IsNullOrWhiteSpace(part.Key)
                    ? part.Key
                    : Catalog.GetPartId(locationIndex, index);
                result.Add(key);
            }

            return result;
        }

        public static IEnumerable<string> GetLevelsForPartOfDay(int locationIndex, string partOfDayKey)
        {
            if (!HasCatalog || string.IsNullOrWhiteSpace(partOfDayKey) || !Catalog.TryGetLocation(locationIndex, out var location))
            {
                return Array.Empty<string>();
            }

            var parts = location.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();
            for (int index = 0; index < parts.Count; index++)
            {
                var part = parts[index];
                var partId = Catalog.GetPartId(locationIndex, index);

                if (string.Equals(part.Key, partOfDayKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(partId, partOfDayKey, StringComparison.OrdinalIgnoreCase))
                {
                    return part.Levels?
                               .OrderBy(level => level.Order)
                               .Select(level => HierarchicalLevelCatalog.NormalizeLevelKey(level.Address))
                               .Where(key => !string.IsNullOrEmpty(key))
                               .ToList()
                           ?? new List<string>();
                }
            }

            return Array.Empty<string>();
        }

        public static string GetLocationKey(int locationIndex)
        {
            if (!HasCatalog || locationIndex < 0 || locationIndex >= Catalog.LocationCount)
            {
                return string.Empty;
            }

            return Catalog.GetLocationId(locationIndex) ?? string.Empty;
        }

        public static int GetLocationIndex()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (string.IsNullOrWhiteSpace(currentLevel))
            {
                return 0;
            }

            if (!TryFindDescriptor(currentLevel, out var descriptor))
            {
                return 0;
            }

            return descriptor.LocationIndex;
        }

        public static bool IsLevelOpen(string levelKey)
        {
            if (!TryResolveProgressKey(levelKey, out var progressKey))
            {
                return false;
            }

            return Progress.IsLevelUnlocked(progressKey);
        }

        public static int GetLevelStars(string levelKey)
        {
            if (!TryResolveProgressKey(levelKey, out var progressKey))
            {
                return 0;
            }

            return Progress.GetStars(progressKey);
        }

        public static string GetLocationName()
        {
            var index = GetLocationIndex();
            var infos = LocationInfoList?.locations ?? Array.Empty<LocationInfoModel>();

            if (index >= 0 && index < infos.Length && !string.IsNullOrWhiteSpace(infos[index]?.name))
            {
                return infos[index].name;
            }

            if (HasCatalog && Catalog.TryGetLocation(index, out var location))
            {
                return location.Key ?? $"Location {index + 1}";
            }

            return string.Empty;
        }

        public static string GetCurrentPartOfDay()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (string.IsNullOrWhiteSpace(currentLevel))
            {
                return string.Empty;
            }

            if (!TryFindDescriptor(currentLevel, out var descriptor))
            {
                return string.Empty;
            }

            return descriptor.PartId;
        }

        public static void OnEnable()
        {
            GameEventsManager.OnLevelCompleted += HandleLevelCompleted;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnLevelCompleted -= HandleLevelCompleted;
        }

    private static void HandleLevelCompleted(int _, int stars)
        {
            var playerData = GameDataManager.PlayerData;
            if (playerData == null)
            {
                return;
            }

            var currentLevel = playerData.CurrentLevel;
            if (string.IsNullOrWhiteSpace(currentLevel))
            {
                return;
            }

            if (!TryResolveProgressKey(currentLevel, out var progressKey))
            {
                return;
            }

            var snapshot = playerData.Progress;
            var clampedStars = Mathf.Clamp(stars, 0, LevelProgressEntry.MaxStars);

            if (!snapshot.TryGet(progressKey, out var entry))
            {
                entry = new LevelProgressEntry(progressKey, true, 0);
            }

            snapshot = snapshot.Set(entry.ApplyStars(clampedStars));

            if (TryGetNextProgressKey(progressKey, out var nextKey))
            {
                if (!snapshot.TryGet(nextKey, out var nextEntry) || !nextEntry.IsUnlocked)
                {
                    var unlocked = new LevelProgressEntry(nextKey, true, nextEntry?.Stars ?? 0);
                    snapshot = snapshot.Set(unlocked);
                }
            }

            playerData.Progress = snapshot;
            GameDataManager.SaveData();
        }

        private static bool TryGetNextProgressKey(LevelProgressKey current, out LevelProgressKey next)
        {
            next = default;

            if (!HasCatalog)
            {
                return false;
            }

            if (!Catalog.TryResolveLocationId(current.LocationId, out var locationIndex))
            {
                return false;
            }

            if (!Catalog.TryGetPart(locationIndex, current.PartOfDayId, out var partIndex, out var partEntry))
            {
                return false;
            }

            var orderedLevels = partEntry.Levels?.OrderBy(level => level.Order).ToList()
                                ?? new List<HierarchicalLevelCatalog.LevelEntry>();
            var nextLevelIndex = current.LevelIndex + 1;
            if (nextLevelIndex < orderedLevels.Count)
            {
                next = new LevelProgressKey(
                    current.LocationId,
                    Catalog.GetPartId(locationIndex, partIndex),
                    nextLevelIndex);
                return true;
            }

            var location = Catalog.Locations[locationIndex];
            for (int i = partIndex + 1; i < location.PartsOfDay.Count; i++)
            {
                var candidate = location.PartsOfDay[i];
                var candidateLevels = candidate.Levels?.OrderBy(level => level.Order).ToList()
                                      ?? new List<HierarchicalLevelCatalog.LevelEntry>();
                if (candidateLevels.Count == 0)
                {
                    continue;
                }

                var partId = Catalog.GetPartId(locationIndex, i);
                next = new LevelProgressKey(current.LocationId, partId, 0);
                return true;
            }

            for (int nextLocationIndex = locationIndex + 1; nextLocationIndex < Catalog.LocationCount; nextLocationIndex++)
            {
                var locationEntry = Catalog.Locations[nextLocationIndex];
                var parts = locationEntry.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();

                for (int i = 0; i < parts.Count; i++)
                {
                    var candidate = parts[i];
                    var candidateLevels = candidate.Levels?.OrderBy(level => level.Order).ToList()
                                          ?? new List<HierarchicalLevelCatalog.LevelEntry>();
                    if (candidateLevels.Count == 0)
                    {
                        continue;
                    }

                    var locationId = Catalog.GetLocationId(nextLocationIndex);
                    var partId = Catalog.GetPartId(nextLocationIndex, i);
                    next = new LevelProgressKey(locationId, partId, 0);
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindDescriptor(string identifier, out HierarchicalLevelCatalog.LevelDescriptor descriptor)
        {
            descriptor = default;

            if (!HasCatalog)
            {
                return false;
            }

            return LevelCatalogService.TryFindLevel(identifier, out descriptor);
        }

        private static bool TryResolveProgressKey(string levelKey, out LevelProgressKey progressKey)
        {
            progressKey = default;

            if (!TryFindDescriptor(levelKey, out var descriptor))
            {
                return false;
            }

            progressKey = new LevelProgressKey(descriptor.LocationId, descriptor.PartId, descriptor.LevelIndex);
            return true;
        }

        private static List<HierarchicalLevelCatalog.LevelDescriptor> EnumerateDescriptors()
        {
            if (!HasCatalog)
            {
                return new List<HierarchicalLevelCatalog.LevelDescriptor>();
            }

            return Catalog.EnumerateLevels()
                .OrderBy(level => level.LocationIndex)
                .ThenBy(level => level.PartIndex)
                .ThenBy(level => level.LevelIndex)
                .ToList();
        }

        private static int GetSequentialIndex(HierarchicalLevelCatalog.LevelDescriptor descriptor)
        {
            var descriptors = EnumerateDescriptors();
            for (int index = 0; index < descriptors.Count; index++)
            {
                if (string.Equals(descriptors[index].LevelKey, descriptor.LevelKey, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CalculateStarsToOpenNextLocation()
        {
            if (!HasCatalog)
            {
                return 0;
            }

            var openedCount = OpenedLocations.Count;
            if (openedCount >= Catalog.LocationCount)
            {
                return 0;
            }

            var requiredStars = CalculateMaxStarsForLocation(openedCount) - _starUnlockOffset;
            if (requiredStars <= 0)
            {
                return 0;
            }

            var currentStars = Progress.Entries.Sum(entry => entry.Stars);
            return Math.Max(requiredStars - currentStars, 0);
        }

        private static int CalculateMaxStarsForLocation(int locationIndex)
        {
            if (!HasCatalog || !Catalog.TryGetLocation(locationIndex, out var location))
            {
                return 0;
            }

            var parts = location.PartsOfDay ?? Array.Empty<HierarchicalLevelCatalog.PartOfDayEntry>();
            var levelCount = parts.Sum(part => part.Levels?.Count ?? 0);
            return levelCount * LevelProgressEntry.MaxStars;
        }

        private static List<LocationInfoModel> BuildOpenedLocations()
        {
            var result = new List<LocationInfoModel>();

            if (!HasCatalog)
            {
                return result;
            }

            var infos = LocationInfoList?.locations ?? Array.Empty<LocationInfoModel>();
            var progress = Progress;

            for (int index = 0; index < Catalog.LocationCount; index++)
            {
                var locationId = Catalog.GetLocationId(index);
                var isUnlocked = progress.EnumerateLocation(locationId).Any(entry => entry.IsUnlocked);

                if (!isUnlocked && index == 0)
                {
                    isUnlocked = true;
                }

                if (!isUnlocked)
                {
                    break;
                }

                var info = index < infos.Length
                    ? infos[index]
                    : new LocationInfoModel
                    {
                        name = Catalog.Locations[index].Key ?? $"Location {index + 1}",
                        image = string.Empty
                    };

                result.Add(info);
            }

            return result;
        }

        private static async Task InitLocationsList()
        {
            try
            {
                var asset = await Addressables.LoadAssetAsync<TextAsset>(Consts.Locations).Task;
                if (asset != null)
                {
                    LocationInfoList = JsonUtility.FromJson<LocationInfoList>(asset.text) ?? new LocationInfoList();
                }
                else
                {
                    LocationInfoList = new LocationInfoList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LevelManager] Failed to load location info: {ex.Message}");
                LocationInfoList = new LocationInfoList();
            }
        }
    }
}
