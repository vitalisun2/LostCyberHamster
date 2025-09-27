using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Assets.Scripts.Common.Models;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Task = System.Threading.Tasks.Task;

namespace Assets.Scripts.System
{
    public static class LevelManager
    {
        public static LocationInfoList LocationInfoList { get; private set; } = new();
        public static List<Common.Models.LocationInfo> OpenedLocations => GetOpenedLocations();

        private static ILevelCatalog Catalog => LevelCatalogService.Current;
        private static int LevelsPerLocation => Catalog.LevelsPerLocation > 0 ? Catalog.LevelsPerLocation : 4;

        /// <summary>
        /// Возвращает список открытых локаций.
        /// </summary>
        /// <returns></returns>
        private static List<Common.Models.LocationInfo> GetOpenedLocations()
        {
            return LocationInfoList?.locations?.Where((location, index) =>
            {
                var firstLevelName = Catalog.GetLevelsForLocation(index)
                    .Select(NormalizeLevelKey)
                    .FirstOrDefault(name => !string.IsNullOrEmpty(name));

                if (string.IsNullOrEmpty(firstLevelName) && !LevelCatalogService.IsHierarchical)
                {
                    int firstLevel = index * LevelsPerLocation + 1;
                    firstLevelName = NormalizeLevelKey(Catalog.GetLevelName(firstLevel));
                }

                return !string.IsNullOrEmpty(firstLevelName) &&
                       GameDataManager.PlayerData.OpenedLevels.ContainsKey(firstLevelName);
            }).ToList();
        }

        public static int StarsToOpenNewLocation => OpenedLocations.Count * LevelsPerLocation * 3 - 2;

        /// <summary>
        /// Проверяет, открыты ли все локации.
        /// </summary>
        /// <returns></returns>
        private static bool AreAllLocationsOpened()
        {
            return LocationInfoList.locations
                .Select((location, index) =>
                {
                    var firstLevelName = Catalog.GetLevelsForLocation(index)
                        .Select(NormalizeLevelKey)
                        .FirstOrDefault(name => !string.IsNullOrEmpty(name));

                    if (string.IsNullOrEmpty(firstLevelName) && !LevelCatalogService.IsHierarchical)
                    {
                        int firstLevel = index * LevelsPerLocation + 1;
                        firstLevelName = NormalizeLevelKey(Catalog.GetLevelName(firstLevel));
                    }
                    return !string.IsNullOrEmpty(firstLevelName) &&
                           GameDataManager.PlayerData.OpenedLevels.ContainsKey(firstLevelName);
                })
                .All(isOpen => isOpen);
        }



        public static async Task Init()
        {
            await InitLocationsList();
        }

        public static int GetCurrentLevelNumber()
        {
            var numberPart = GameDataManager.PlayerData.CurrentLevel.Split('_')[1];

            if (!int.TryParse(numberPart, out var result)
                || numberPart.Length != 2)
            {
                Debug.LogError("Invalid level name");
                return -1;
            }

            return result;
        }

        public static async Task LoadLevelData()
        {
            await LevelDataProvider.LoadLevelData();
        }

        public static int GetLevelNumber(int locationNumber, PartOfDayEnum partOfDay)
        {
            return Catalog.GetLevelNumber(locationNumber, partOfDay);
        }

        public static string GetLevelName(int locationIndex, string partOfDayKey)
        {
            if (string.IsNullOrWhiteSpace(partOfDayKey))
            {
                return string.Empty;
            }

            var levels = Catalog.GetLevelsForPartOfDay(locationIndex, partOfDayKey);
            var firstLevel = levels?.FirstOrDefault();
            return NormalizeLevelKey(firstLevel);
        }

        private static string NormalizeLevelKey(string? levelAddress)
        {
            if (string.IsNullOrWhiteSpace(levelAddress))
            {
                return string.Empty;
            }

            var normalized = Path.GetFileNameWithoutExtension(levelAddress);
            return string.IsNullOrWhiteSpace(normalized) ? levelAddress : normalized;
        }

        public static bool TryParseLevelNumber(string levelKey, out int levelNumber)
        {
            levelNumber = -1;

            if (string.IsNullOrWhiteSpace(levelKey) || !levelKey.StartsWith("level_"))
            {
                return false;
            }

            var suffix = levelKey.Substring(6);
            if (!int.TryParse(suffix, out levelNumber))
            {
                levelNumber = -1;
                return false;
            }

            return suffix.Length == 2;
        }

        public static bool TryResolveLevelKey(string levelKey, out int locationIndex, out string partOfDayKey, out int levelOrder)
        {
            locationIndex = -1;
            partOfDayKey = string.Empty;
            levelOrder = -1;

            var normalizedKey = NormalizeLevelKey(levelKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return false;
            }

            if (LevelCatalogService.Hierarchical is { } hierarchical)
            {
                for (int locIndex = 0; locIndex < hierarchical.Locations.Count; locIndex++)
                {
                    var location = hierarchical.Locations[locIndex];
                    foreach (var part in location.PartsOfDay)
                    {
                        int order = 0;
                        foreach (var level in part.Levels.OrderBy(l => l.Order))
                        {
                            order++;
                            var key = NormalizeLevelKey(level.Address);
                            if (string.Equals(key, normalizedKey, StringComparison.OrdinalIgnoreCase))
                            {
                                locationIndex = locIndex;
                                partOfDayKey = part.Key;
                                levelOrder = order - 1;
                                return true;
                            }
                        }
                    }
                }
            }

            if (TryResolveLegacyLevel(normalizedKey, out locationIndex, out var partEnum, out levelOrder))
            {
                partOfDayKey = partEnum.ToString();
                return true;
            }

            return false;
        }

        private static bool TryResolveLegacyLevel(string normalizedKey, out int locationIndex, out PartOfDayEnum partOfDay, out int levelOrder)
        {
            locationIndex = -1;
            partOfDay = default;
            levelOrder = -1;

            if (!normalizedKey.StartsWith("level_") || !int.TryParse(normalizedKey.Substring(6), out var levelNumber))
            {
                return false;
            }

            partOfDay = (PartOfDayEnum)((levelNumber - 1) % LevelsPerLocation + 1);
            locationIndex = (levelNumber - 1) / LevelsPerLocation;
            levelOrder = ((levelNumber - 1) % LevelsPerLocation);
            return true;
        }


        public static string GetLevelName(int locationNumber, PartOfDayEnum partOfDay)

        {
            return Catalog.GetLevelName(locationNumber, partOfDay);
        }

        public static bool TryGetNextLevelKey(string currentLevelKey, out string nextLevelKey)
        {
            nextLevelKey = string.Empty;

            if (string.IsNullOrWhiteSpace(currentLevelKey))
            {
                return false;
            }

            if (LevelCatalogService.IsHierarchical && LevelCatalogService.Hierarchical is { } hierarchical)
            {
                return TryGetNextLevelKeyHierarchical(hierarchical, currentLevelKey, out nextLevelKey);
            }

            return TryGetNextLevelKeyLegacy(currentLevelKey, out nextLevelKey);
        }

        public static int GetTotalLevelsCount()
        {
            if (LevelCatalogService.Hierarchical is { } hierarchical)
            {
                return hierarchical.Locations.Sum(location => location.PartsOfDay.Sum(part => part.Levels.Count));
            }

            int totalLocations = LocationInfoList.locations?.Length ?? 0;
            return totalLocations * LevelsPerLocation;
        }

        private static bool TryGetNextLevelKeyLegacy(string currentLevelKey, out string nextLevelKey)
        {
            nextLevelKey = string.Empty;
            var normalized = NormalizeLevelKey(currentLevelKey);
            if (!normalized.StartsWith("level_") || !int.TryParse(normalized.Substring(6), out var currentNumber))
            {
                return false;
            }

            int nextNumber = currentNumber + 1;
            int totalLevels = GetTotalLevelsCount();
            if (nextNumber > totalLevels)
            {
                return false;
            }

            nextLevelKey = Catalog.GetLevelName(nextNumber);
            return true;
        }

        private static bool TryGetNextLevelKeyHierarchical(HierarchicalLevelCatalog catalog, string currentLevelKey, out string nextLevelKey)
        {
            nextLevelKey = string.Empty;
            var normalized = NormalizeLevelKey(currentLevelKey);

            for (int locationIndex = 0; locationIndex < catalog.Locations.Count; locationIndex++)
            {
                var orderedLevels = catalog.GetLevelsForLocation(locationIndex)
                    .Select(NormalizeLevelKey)
                    .Where(key => !string.IsNullOrEmpty(key))
                    .ToList();

                if (orderedLevels.Count == 0)
                {
                    continue;
                }

                var currentIndex = orderedLevels.FindIndex(key => string.Equals(key, normalized, StringComparison.OrdinalIgnoreCase));
                if (currentIndex < 0)
                {
                    continue;
                }

                if (currentIndex + 1 < orderedLevels.Count)
                {
                    nextLevelKey = orderedLevels[currentIndex + 1];
                    return true;
                }

                // move to next location
                for (int nextLocation = locationIndex + 1; nextLocation < catalog.Locations.Count; nextLocation++)
                {
                    var nextLocationLevels = catalog.GetLevelsForLocation(nextLocation)
                        .Select(NormalizeLevelKey)
                        .Where(key => !string.IsNullOrEmpty(key))
                        .ToList();

                    if (nextLocationLevels.Count == 0)
                    {
                        continue;
                    }

                    nextLevelKey = nextLocationLevels[0];
                    return true;
                }

                break;
            }

            return false;
        }

        public static IEnumerable<string> GetPartOfDayKeys(int locationIndex)
        {
            return Catalog.GetPartOfDayKeys(locationIndex) ?? Array.Empty<string>();
        }

        public static IEnumerable<string> GetLevelsForPartOfDay(int locationIndex, string partOfDayKey)
        {
            return Catalog.GetLevelsForPartOfDay(locationIndex, partOfDayKey)?.Select(NormalizeLevelKey).Where(key => !string.IsNullOrEmpty(key)) ?? Array.Empty<string>();
        }

        public static string? GetLocationKey(int locationIndex)
        {
            if (LevelCatalogService.Hierarchical is { } hierarchical)
            {
                return hierarchical.GetLocationKey(locationIndex);
            }

            return null;
        }

        /// <summary>
        /// Инициализирует список локаций и сверяет количество реальных уровней с ожидаемым.
        /// При несоответствии выбрасывает исключение.
        /// </summary>
        private static async Task InitLocationsList()
        {
            Debug.Log("Init locations list");

            var realLevelNames = await LevelDataProvider.GetAllLevelNamesAsync();
            int realLevelsCount = realLevelNames.Count;

            var asset = await Addressables.LoadAssetAsync<TextAsset>(Consts.Locations).Task;
            LocationInfoList = JsonUtility.FromJson<LocationInfoList>(asset.text);

            int totalLocations = LocationInfoList.locations?.Length ?? 0;
            int expectedLevelsCount = totalLocations * LevelsPerLocation;

            if (realLevelsCount != expectedLevelsCount)
            {
                var message = $"[InitLocationsList] Несоответствие: {realLevelsCount} реальных уровней, " +
                              $"а ожидается {expectedLevelsCount} (на основе {totalLocations} локаций).";
                Debug.LogError(message);
                throw new Exception(message);
            }

            Debug.Log("Locations list initialized");
        }


        /// <summary>
        /// Определяет индекс локации на основе PlayerData.CurrentLevel.
        /// Возвращает -1 при ошибке разбора или выходе за границы.
        /// Пример: уровни level_01..level_04 → индекс 0, level_05..level_08 → индекс 1 и т.д.
        /// </summary>
        public static int GetLocationIndex()
        {
            string levelName = GameDataManager.PlayerData.CurrentLevel;

            // Разделяем по символу '_', ожидая минимум две части: ["level", "XX"]
            string[] parts = levelName.Split('_');

            // Пытаемся считать числовую часть (parts[1], если она существует)
            if (!int.TryParse(parts.ElementAtOrDefault(1), out int numericLevel))
            {
                Debug.LogError($"[GetLocationIndex] Не удалось распарсить номер уровня из '{levelName}'. Ожидается формат 'level_XX'.");
                return -1;
            }

            // Уровень должен быть >= 1
            if (numericLevel < 1)
            {
                Debug.LogError($"[GetLocationIndex] Недопустимый номер уровня: {numericLevel} (уровень '{levelName}').");
                return -1;
            }

            // Legacy formula splits sequential numbers by LevelsPerLocation to obtain location index.
            int locationIndex = (numericLevel - 1) / LevelsPerLocation;

            // Проверяем, не вышли ли мы за общее число локаций
            int totalLocations = LocationInfoList.locations?.Length ?? 0;
            if (locationIndex >= totalLocations)
            {
                Debug.LogError($"[GetLocationIndex] Вычисленный индекс локации {locationIndex} выходит за пределы (0..{totalLocations - 1}).");
                return -1;
            }

            return locationIndex;
        }


        public static bool IsLevelOpen(string level)
        {
            return GameDataManager.PlayerData.OpenedLevels.ContainsKey(level);
        }

        public static int GetLevelStars(string level)
        {
            GameDataManager.PlayerData.OpenedLevels.TryGetValue(level, out int stars);
            return stars;
        }

        public static string GetLocationName()
        {
            var locationIndex = GetLocationIndex();

            return LocationInfoList.locations[locationIndex].name;
        }

        /// <summary>
        /// Определяет часть дня по имени уровня в формате "level_XX" на основе имени текущего уровня.
        /// </summary>
        /// <returns>Часть дня в виде строки ("Morning", "Afternoon", "Evening", "Night").</returns>
        public static string GetCurrentPartOfDay()
        {
            var levelName = GameDataManager.PlayerData.CurrentLevel;

            if (LevelCatalogService.IsHierarchical)
            {
                if (TryResolveLevelKey(levelName, out _, out var partKey, out _))
                {
                    return partKey;
                }

                return "Invalid level format";
            }

            // Проверяем, начинается ли строка с "level_" и извлекаем номер уровня
            if (!levelName.StartsWith("level_") || !int.TryParse(levelName.Substring(6), out int levelNumber))
            {
                return "Invalid level format"; // на случай, если формат некорректный
            }

            // Определяем, какой это уровень в локации (остаток от деления на LevelsPerLocation)
            int levelInLocation = (levelNumber - 1) % LevelsPerLocation + 1;

            // Преобразуем номер уровня в части дня через enum
            if (Enum.IsDefined(typeof(PartOfDayEnum), levelInLocation))
            {
                return ((PartOfDayEnum)levelInLocation).ToString();
            }
            else
            {
                return "Invalid level number"; // на случай, если уровень вне диапазона 1-4
            }
        }
        public static void OnEnable()
        {
            GameEventsManager.OnLevelCompleted += OnLevelComplited;
        }

        private static void OnLevelComplited(int levelNumber, int stars)
        {
            if(GameDataManager.PlayerData.LevelStars[levelNumber-1] < stars){
                GameDataManager.PlayerData.LevelStars[levelNumber-1] = stars;
            }
            OpenNextLevel(levelNumber);
        }

        /// <summary>
        /// Разблокирует следующий уровень (если хватает звёзд для новой локации),
        /// но не меняет текущий уровень и не загружает сцену.
        /// </summary>
        /// <param name="levelNumber">Номер только что завершённого уровня.</param>
        private static void OpenNextLevel(int levelNumber)
        {
            Debug.Log("OpenNextLevel: Unlock the next level without changing CurrentLevel.");

            // Расчёт следующего уровня (после пройденного)
            int nextLevelNumber = levelNumber + 1;

            // 1) Проверяем, не вышли ли мы за общее число уровней
            int maxLevelsCount = GetTotalLevelsCount();
            if (nextLevelNumber > maxLevelsCount)
            {
                Debug.Log("All levels are completed; no further levels to unlock.");
                return;
            }

            // 2) Собираем инфо по звёздам для проверки открытия новой локации
            int playerStars = GameDataManager.PlayerData.OpenedLevels.Values.Sum();
            bool canOpenNewLocation = playerStars >= StarsToOpenNewLocation;

            // Например, если деление на LevelsPerLocation без остатка — это начало новой локации
            bool isNewLocationLevel = ((nextLevelNumber - 1) % LevelsPerLocation == 0);

            // 3) Логика "можем ли мы открыть следующий уровень":
            //    - Если это начало новой локации (isNewLocationLevel) и хватает звёзд (canOpenNewLocation),
            //      ИЛИ если это не начало локации, то просто разблокируем
            if ((isNewLocationLevel && canOpenNewLocation) || !isNewLocationLevel)
            {
                // Убедимся, что в PlayerData.LevelStars есть слот для nextLevelNumber
                // (Если там только 3 записи, а мы пытаемся открыть 4-й уровень, надо добавить элемент)
                if (GameDataManager.PlayerData.LevelStars.Count < nextLevelNumber)
                {
                    while (GameDataManager.PlayerData.LevelStars.Count < nextLevelNumber)
                    {
                        // По умолчанию ставим 0 звёзд
                        GameDataManager.PlayerData.LevelStars.Add(0);
                    }
                }

                // В этот момент уровень считается "открытым", т.к. он появился в словаре OpenedLevels
                Debug.Log($"Unlocked next level: level_{nextLevelNumber:D2}");

                // 4) Сохраняем данные игрока
                GameDataManager.SaveData();
            }
            else
            {
                Debug.Log($"Next level is location start, but not enough stars. Remains locked.");
            }
        }


        public static void OnDisable()
        {
            GameEventsManager.OnLevelCompleted -= OnLevelComplited;
        }
    }
}
