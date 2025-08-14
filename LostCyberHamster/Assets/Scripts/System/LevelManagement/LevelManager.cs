using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Возвращает список открытых локаций.
        /// </summary>
        /// <returns></returns>
        private static List<Common.Models.LocationInfo> GetOpenedLocations()
        {
            return LocationInfoList?.locations?.Where((location, index) =>
            {
                // Calculate the first level number for the location based on its index (0-based)
                int firstLevel = index * 4 + 1;
                string levelKey = $"level_{firstLevel:D2}"; // Format as "level_01", "level_05", etc.

                // Check if the first level in this location's range is open
                return GameDataManager.PlayerData.OpenedLevels.ContainsKey(levelKey);
            }).ToList();

        }

        public static int StarsToOpenNewLocation => OpenedLocations.Count * 4 * 3 - 2;

        /// <summary>
        /// Проверяет, открыты ли все локации.
        /// </summary>
        /// <returns></returns>
        private static bool AreAllLocationsOpened()
        {
            return LocationInfoList.locations
                .Select((location, index) =>
                {
                    int firstLevel = index * 4 + 1;
                    string levelKey = $"level_{firstLevel:D2}";
                    return GameDataManager.PlayerData.OpenedLevels.ContainsKey(levelKey);
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
            // Calculate the level number
            int levelNumber = locationNumber * 4 + (int)partOfDay;
            return levelNumber;
        }

        public static string GetLevelName(int locationNumber, PartOfDayEnum partOfDay)
        {
            // Calculate the level number
            int levelNumber = GetLevelNumber(locationNumber, partOfDay);

            // Format the level number with leading zeros to ensure two digits
            string levelName = "level_" + levelNumber.ToString("D2");

            return levelName;
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
            int expectedLevelsCount = totalLocations * 4;

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

            // Каждая локация содержит по 4 уровня, поэтому смещаем на 1, чтобы 1..4 → 0, 5..8 → 1 и т.д.
            int locationIndex = (numericLevel - 1) / 4;

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

            // Проверяем, начинается ли строка с "level_" и извлекаем номер уровня
            if (!levelName.StartsWith("level_") || !int.TryParse(levelName.Substring(6), out int levelNumber))
            {
                return "Invalid level format"; // на случай, если формат некорректный
            }

            // Определяем, какой это уровень в локации (остаток от деления на 4)
            int levelInLocation = (levelNumber - 1) % 4 + 1;

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
            int maxLevelsCount = LocationInfoList.locations.Count() * 4;
            if (nextLevelNumber > maxLevelsCount)
            {
                Debug.Log("All levels are completed; no further levels to unlock.");
                return;
            }

            // 2) Собираем инфо по звёздам для проверки открытия новой локации
            int playerStars = GameDataManager.PlayerData.OpenedLevels.Values.Sum();
            bool canOpenNewLocation = playerStars >= StarsToOpenNewLocation;

            // Например, если (nextLevelNumber - 1) % 4 == 0 => это 4-й, 8-й, 12-й уровень (то есть начало новой локации)
            bool isNewLocationLevel = ((nextLevelNumber - 1) % 4 == 0);

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
