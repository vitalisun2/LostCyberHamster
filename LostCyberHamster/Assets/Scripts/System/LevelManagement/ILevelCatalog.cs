using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Defines operations for retrieving legacy level data without exposing arithmetic details.
    /// </summary>
    public interface ILevelCatalog
    {
        /// <summary>
        /// Gets the amount of legacy levels contained in a single location.
        /// </summary>
        int LevelsPerLocation { get; }

        /// <summary>
        /// Returns the formatted level name (e.g. "level_01") for the provided sequential number.
        /// </summary>
        string GetLevelName(int levelNumber);

        /// <summary>
        /// Returns the formatted level name for the specified location index and part of day.
        /// </summary>
        string GetLevelName(int locationIndex, PartOfDayEnum partOfDay);

        /// <summary>
        /// Returns the sequential level number for a location index and part of day.
        /// </summary>
        int GetLevelNumber(int locationIndex, PartOfDayEnum partOfDay);

        /// <summary>
        /// Enumerates level names that belong to the specified location index.
        /// </summary>
        IEnumerable<string> GetLevelsForLocation(int locationIndex);
        /// <summary>
        /// Returns identifiers of parts of day that are available for the location index.
        /// </summary>
        IEnumerable<string> GetPartOfDayKeys(int locationIndex);

        /// <summary>
        /// Returns level addresses that belong to a particular part of day within the location.
        /// </summary>
        IEnumerable<string> GetLevelsForPartOfDay(int locationIndex, string partOfDayKey);

    }
}
