using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Legacy implementation that replicates the existing arithmetic-based level mapping.
    /// </summary>
    public class LegacyLevelCatalog : ILevelCatalog
    {
        private const int LegacyLevelsPerLocation = 4;

        public int LevelsPerLocation => LegacyLevelsPerLocation;

        public string GetLevelName(int levelNumber)
        {
            return $"level_{levelNumber:D2}";
        }

        public string GetLevelName(int locationIndex, PartOfDayEnum partOfDay)
        {
            var levelNumber = GetLevelNumber(locationIndex, partOfDay);
            return GetLevelName(levelNumber);
        }

        public int GetLevelNumber(int locationIndex, PartOfDayEnum partOfDay)
        {
            return locationIndex * LegacyLevelsPerLocation + (int)partOfDay;
        }

        public IEnumerable<string> GetLevelsForLocation(int locationIndex)
        {
            var firstLevelNumber = GetFirstLevelNumber(locationIndex);

            for (int offset = 0; offset < LegacyLevelsPerLocation; offset++)
            {
                yield return GetLevelName(firstLevelNumber + offset);
            }
        }

        public IEnumerable<string> GetPartOfDayKeys(int locationIndex)
        {
            foreach (PartOfDayEnum part in Enum.GetValues(typeof(PartOfDayEnum)))
            {
                yield return part.ToString();
            }
        }

        public IEnumerable<string> GetLevelsForPartOfDay(int locationIndex, string partOfDayKey)
        {
            if (!TryParsePartOfDay(partOfDayKey, out var partOfDay))
            {
                yield break;
            }

            yield return GetLevelName(locationIndex, partOfDay);
        }

        private static bool TryParsePartOfDay(string key, out PartOfDayEnum partOfDay)
        {
            return Enum.TryParse(key, true, out partOfDay);
        }

        private static int GetFirstLevelNumber(int locationIndex)
        {
            return locationIndex * LegacyLevelsPerLocation + 1;
        }
    }
}
