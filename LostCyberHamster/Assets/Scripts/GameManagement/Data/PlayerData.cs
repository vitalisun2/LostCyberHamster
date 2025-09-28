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
        public int Money;
        public int Crystals;
        public int AppliedSkinId = 0;
        public List<int> PurchasedSkinIds = new() { 0 };
        public string CurrentLevel = "level_01";
        public string DailyTasksRefreshDate;
        public List<Quest> DailyTasks;

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
                if (_progressSnapshot == LevelProgressSnapshot.Empty && _serializedProgress.Count > 0)
                {
                    _progressSnapshot = DeserializeSnapshot(_serializedProgress);
                }

                return _progressSnapshot;
            }
            set
            {
                _progressSnapshot = value ?? LevelProgressSnapshot.Empty;
                _serializedProgress = SerializeSnapshot(_progressSnapshot);
            }
        }

        public string ToJson()
        {
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
            else
            {
                _progressSnapshot = LevelProgressSnapshot.Empty;
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
