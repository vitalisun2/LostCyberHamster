using System;
using System.Collections.Generic;
using System.Linq;
using GameManagement.Progress;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement
{
    [Serializable]
    public class PlayerData
    {
        private const int LegacyLevelsPerLocation = 4;

        public int Money;
        public int Crystals;
        public int AppliedSkinId = 0;
        public List<int> PurchasedSkinIds = new() { 0 };
        public string CurrentLevel = "level_01";
        public string DailyTasksRefreshDate;
        public List<Quest> DailyTasks;

        /// <summary>
        /// Legacy field that keeps stars per sequential level ("level_XX").
        /// We maintain the field for backward compatibility and populate it from the typed snapshot before serialisation.
        /// </summary>
        public List<int> LevelStars = new() { 0 };

        [SerializeField]
        private List<SerializableLevelProgressEntry> _serializedProgress = new();

        [NonSerialized]
        private LevelProgressSnapshot _progressSnapshot = LevelProgressSnapshot.Empty;

        public Dictionary<string, bool> ComplitedStorylineQuests = new();
        public string LastSaveDate = DateTime.MinValue.ToString("o");
        public bool IsFirstLaunch = true;

        public LevelProgressSnapshot Progress
        {
            get
            {
                if (_progressSnapshot == LevelProgressSnapshot.Empty)
                {
                    if (_serializedProgress.Count > 0)
                    {
                        _progressSnapshot = DeserializeSnapshot(_serializedProgress);
                    }
                    else if (LevelStars != null && LevelStars.Count > 0)
                    {
                        _progressSnapshot = BuildSnapshotFromLegacy(LevelStars, LegacyLevelsPerLocation);
                        _serializedProgress = SerializeSnapshot(_progressSnapshot);
                    }
                }

                return _progressSnapshot;
            }
            set
            {
                _progressSnapshot = value ?? LevelProgressSnapshot.Empty;
                _serializedProgress = SerializeSnapshot(_progressSnapshot);
            }
        }

        public Dictionary<string, int> OpenedLevels => LevelStars
            .Select((stars, index) => new { LevelName = $"level_{(index + 1):D2}", Stars = stars })
            .ToDictionary(x => x.LevelName, x => x.Stars);

        public string ToJson()
        {
            SyncLegacyStars();
            return JsonUtility.ToJson(this);
        }

        public static PlayerData FromJson(string json)
        {
            var data = JsonUtility.FromJson<PlayerData>(json);
            data.RestoreSnapshot();
            return data;
        }

        private void RestoreSnapshot()
        {
            if (_serializedProgress != null && _serializedProgress.Count > 0)
            {
                _progressSnapshot = DeserializeSnapshot(_serializedProgress);
            }
            else if (LevelStars != null && LevelStars.Count > 0)
            {
                _progressSnapshot = BuildSnapshotFromLegacy(LevelStars, LegacyLevelsPerLocation);
                _serializedProgress = SerializeSnapshot(_progressSnapshot);
            }
            else
            {
                _progressSnapshot = LevelProgressSnapshot.Empty;
            }
        }

        private void SyncLegacyStars()
        {
            if (_progressSnapshot == LevelProgressSnapshot.Empty)
            {
                return;
            }

            var orderedEntries = _progressSnapshot.Entries
                .OrderBy(entry => entry.Key.LocationId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Key.PartOfDayId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Key.LevelIndex)
                .ToList();

            if (orderedEntries.Count == 0)
            {
                return;
            }

            if (LevelStars == null)
            {
                LevelStars = new List<int>(orderedEntries.Count);
            }

            if (LevelStars.Count < orderedEntries.Count)
            {
                while (LevelStars.Count < orderedEntries.Count)
                {
                    LevelStars.Add(0);
                }
            }

            for (int i = 0; i < orderedEntries.Count; i++)
            {
                LevelStars[i] = Mathf.Clamp(orderedEntries[i].Stars, 0, LevelProgressEntry.MaxStars);
            }
        }

        private static List<SerializableLevelProgressEntry> SerializeSnapshot(LevelProgressSnapshot snapshot)
        {
            if (snapshot == LevelProgressSnapshot.Empty)
            {
                return new List<SerializableLevelProgressEntry>();
            }

            return snapshot.Entries
                .Select(entry => new SerializableLevelProgressEntry
                {
                    LocationId = entry.Key.LocationId,
                    PartOfDayId = entry.Key.PartOfDayId,
                    LevelIndex = entry.Key.LevelIndex,
                    Stars = entry.Stars,
                    IsUnlocked = entry.IsUnlocked
                })
                .ToList();
        }

        private static LevelProgressSnapshot DeserializeSnapshot(IEnumerable<SerializableLevelProgressEntry> entries)
        {
            if (entries == null)
            {
                return LevelProgressSnapshot.Empty;
            }

            var models = new List<LevelProgressEntry>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.LocationId) || string.IsNullOrWhiteSpace(entry.PartOfDayId))
                {
                    continue;
                }

                var key = new LevelProgressKey(entry.LocationId.Trim(), entry.PartOfDayId.Trim(), Math.Max(0, entry.LevelIndex));
                models.Add(new LevelProgressEntry(key, entry.IsUnlocked, entry.Stars));
            }

            return models.Count == 0
                ? LevelProgressSnapshot.Empty
                : new LevelProgressSnapshot(models);
        }

        private static LevelProgressSnapshot BuildSnapshotFromLegacy(List<int> legacyStars, int levelsPerLocation)
        {
            if (legacyStars == null || legacyStars.Count == 0)
            {
                return LevelProgressSnapshot.Empty;
            }

            if (levelsPerLocation <= 0)
            {
                levelsPerLocation = LegacyLevelsPerLocation;
            }

            var entries = new List<LevelProgressEntry>(legacyStars.Count);

            for (int index = 0; index < legacyStars.Count; index++)
            {
                var locationIndex = index / levelsPerLocation;
                var partOrder = index % levelsPerLocation;
                var locationId = $"location_{locationIndex:D2}";
                var partId = LevelProgressKeyAdapters.ResolvePartOfDayId(partOrder) ?? $"Part_{partOrder:D2}";
                var key = new LevelProgressKey(locationId, partId, 0);
                var stars = Mathf.Clamp(legacyStars[index], 0, LevelProgressEntry.MaxStars);

                // Entry exists in the list => level is at least unlocked.
                var isUnlocked = true;
                entries.Add(new LevelProgressEntry(key, isUnlocked, stars));
            }

            return new LevelProgressSnapshot(entries);
        }

        [Serializable]
        private class SerializableLevelProgressEntry
        {
            public string LocationId;
            public string PartOfDayId;
            public int LevelIndex;
            public bool IsUnlocked;
            public int Stars;
        }
    }
}
