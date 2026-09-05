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
        private static readonly PlayerExperienceService _playerExperienceService = new();
        private static ProgressService _progressService;
        private static HierarchicalLevelCatalog _progressCatalog;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static Func<LevelProgressSnapshot, HierarchicalLevelCatalog, LevelProgressSnapshot> _developmentProgressOverride;
        private static Func<bool> _developmentProgressSaveSuppression;
#endif

        public static LocationInfoList LocationInfoList { get; private set; } = new();

        public static int StarsToOpenNewLocation => CalculateStarsToOpenNextLocation();

        /// <summary>
        /// Возвращает модель прогресса для UI с учётом development override.
        /// </summary>
        public static LevelProgressOverview ProgressOverview => HasCatalog
            ? RequireProgressService().GetOverview(Progress)
            : LevelProgressOverview.Empty;

        /// <summary>
        /// Возвращает модель сохранённого прогресса без development override.
        /// </summary>
        public static LevelProgressOverview SavedProgressOverview => HasCatalog
            ? RequireProgressService().GetOverview(
                GameDataManager.PlayerData?.Progress ??
                LevelProgressSnapshot.Empty)
            : LevelProgressOverview.Empty;

        private static HierarchicalLevelCatalog Catalog => LevelCatalogService.Catalog;

        private static bool HasCatalog => LevelCatalogService.HasCatalog;

        private static LevelProgressSnapshot Progress => GetEffectiveProgress();

        public static async Task Init()
        {
            await InitLocationsList();
        }

        public static async Task LoadLevelData()
        {
            await LevelDataProvider.LoadLevelData();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Подключает внешний dev-only override чтения progress без изменения сохранения игрока.
        /// </summary>
        public static void SetDevelopmentProgressOverride(
            Func<LevelProgressSnapshot, HierarchicalLevelCatalog, LevelProgressSnapshot> progressOverride,
            Func<bool> saveSuppression)
        {
            _developmentProgressOverride = progressOverride;
            _developmentProgressSaveSuppression = saveSuppression;
        }
#endif

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
                string.Equals(d.Address?.Trim(), descriptor.Address?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (index < 0 || index + 1 >= descriptors.Count)
            {
                return false;
            }

            nextLevelKey = descriptors[index + 1].Address?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(nextLevelKey))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Проверяет, является ли уровень последним gameplay-уровнем текущего каталога.
        /// </summary>
        public static bool IsLastAvailableLevel(string levelKey)
        {
            if (!TryFindDescriptor(levelKey, out var descriptor))
            {
                return false;
            }

            var descriptors = EnumerateDescriptors();
            if (descriptors.Count == 0)
            {
                return false;
            }

            var lastDescriptor = descriptors[descriptors.Count - 1];
            return string.Equals(
                descriptor.Address?.Trim(),
                lastDescriptor.Address?.Trim(),
                StringComparison.OrdinalIgnoreCase);
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
                    return Catalog.EnumerateLevels(locationIndex, index)
                        .Select(descriptor => descriptor.Address?.Trim())
                        .Where(address => !string.IsNullOrWhiteSpace(address))
                        .ToList();
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

        /// <summary>
        /// Проверяет готовое состояние доступности уровня.
        /// </summary>
        public static bool IsLevelOpen(string levelKey)
        {
            if (!TryResolveProgressKey(levelKey, out var progressKey))
            {
                return false;
            }

            return ProgressOverview.TryGetLevel(progressKey, out var level)
                ? level.IsUnlocked
                : Progress.IsLevelUnlocked(progressKey);
        }

        /// <summary>
        /// Возвращает готовое количество звёзд уровня.
        /// </summary>
        public static int GetLevelStars(string levelKey)
        {
            if (!TryResolveProgressKey(levelKey, out var progressKey))
            {
                return 0;
            }

            return ProgressOverview.TryGetLevel(progressKey, out var level)
                ? level.Stars
                : Progress.GetStars(progressKey);
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

        /// <summary>
        /// Разрешает координаты текущего уровня в иерархии прогресса.
        /// </summary>
        public static bool TryGetCurrentProgressKey(out LevelProgressKey progressKey)
        {
            progressKey = default;
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;

            return !string.IsNullOrWhiteSpace(currentLevel)
                   && TryResolveProgressKey(currentLevel, out progressKey);
        }

        public static void OnEnable()
        {
            GameEventsManager.OnLevelCompleted += HandleLevelCompleted;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnLevelCompleted -= HandleLevelCompleted;
        }

        /// <summary>
        /// Завершает указанный уровень через штатные progress, XP и checkpoint сервисы.
        /// </summary>
        public static bool CompleteLevel(string levelKey, int stars)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_developmentProgressSaveSuppression?.Invoke() == true)
            {
                return false;
            }
#endif

            // Проверяем player data и разрешаем явный ключ уровня в реальном каталоге.
            var playerData = GameDataManager.PlayerData;
            if (playerData == null ||
                string.IsNullOrWhiteSpace(levelKey) ||
                !HasCatalog ||
                !TryResolveProgressKey(levelKey, out var progressKey))
            {
                return false;
            }

            // Обновляем best stars через существующую progress-логику.
            var progressService = RequireProgressService();
            var updatedSnapshot = progressService.HandleLevelCompleted(
                playerData.Progress,
                progressKey,
                stars);

            // Сохраняем stars и XP до публикации level-up и обновления заданий.
            bool levelChanged = false;
            GameDataManager.ExecuteTransaction(CheckpointReason.LevelCompleted, () =>
            {
                levelChanged = _playerExperienceService.GrantExperienceForImprovedStars(
                    playerData, progressKey, updatedSnapshot, notify: false);
                playerData.Progress = updatedSnapshot;
            }, () => PlayerExperienceService.PublishCommittedLevelChange(levelChanged));
            return true;
        }

        private static void HandleLevelCompleted(int _, int stars)
        {
            // Runtime win завершает текущий выбранный уровень через общий контракт.
            CompleteLevel(
                GameDataManager.PlayerData?.CurrentLevel,
                stars);
        }

        private static LevelProgressSnapshot GetEffectiveProgress()
        {
            var realProgress = GameDataManager.PlayerData?.Progress ?? LevelProgressSnapshot.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return _developmentProgressOverride?.Invoke(realProgress, Catalog) ?? realProgress;
#else
            return realProgress;
#endif
        }

        private static bool TryGetNextProgressKey(LevelProgressKey current, out LevelProgressKey next)
        {
            next = default;

            if (!HasCatalog)
            {
                return false;
            }

            var service = RequireProgressService();
            return service.TryGetNextProgressKey(current, out next);
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

        private static ProgressService RequireProgressService()
        {
            if (!HasCatalog)
            {
                throw new InvalidOperationException("Level catalog is not available.");
            }

            if (_progressService == null || !ReferenceEquals(_progressCatalog, Catalog))
            {
                var policy = new DefaultUnlockPolicy(Catalog, _starUnlockOffset);
                _progressService = new ProgressService(Catalog, policy);
                _progressCatalog = Catalog;
            }

            return _progressService;
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
            if (string.IsNullOrWhiteSpace(descriptor.Address))
            {
                return -1;
            }

            var targetAddress = descriptor.Address.Trim();
            var descriptors = EnumerateDescriptors();
            for (int index = 0; index < descriptors.Count; index++)
            {
                var candidateAddress = descriptors[index].Address?.Trim();
                if (string.Equals(candidateAddress, targetAddress, StringComparison.OrdinalIgnoreCase))
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

            var service = RequireProgressService();
            return service.GetStarsToOpenNextLocation(Progress);
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
