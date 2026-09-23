using System;
using System.Collections.Generic;
using System.Linq;
using GameManagement.Progress;
using Assets.Scripts.System;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;
using Vues.GameCore.ReturnActivities;

namespace GameManagement
{
    [Serializable]
    public class PlayerData
    {
        public int Money;
        public List<string> AppliedWeeklyRewardRunIds = new();
        public List<string> AppliedRewardedRequestIds = new();
        public MonetizationState Monetization = new();
        public int Crystals;
        public int ExperiencePoints;
        public int PlayerLevel = 1;
        public int DevelopmentProgressVersion =
            CharacterDevelopmentService.CurrentProgressVersion;
        public int DevelopmentPoints;
        public List<int> UnlockedSkinIds = new() { 0 };
        public List<int> UnlockedSuperAttackIds = new();
        public List<SuperAttackLevelProgress> SuperAttackLevels = new();
        public int AppliedSkinId = 0;
        public int ActiveSuperAttackId = 0;
        public List<int> PurchasedSkinIds = new() { 0 };
        public string CurrentLevel;
        public List<Quest> QuestStates = new();
        public DailyQuestSetState DailyQuestSet = new();
        public StoryQuestSetState StoryQuestSet = new();
        public ReturnActivityState ReturnActivities = new();

        [SerializeField]
        private List<SerializableLevelProgressEntry> _serializedProgress = new();

        [NonSerialized]
        private LevelProgressSnapshot _progressSnapshot = LevelProgressSnapshot.Empty;

        public string LastSaveDate = DateTime.MinValue.ToString("o");
        public bool IsFirstLaunch = true;
        public bool IsTutorialCompleted;
        public bool IsTutorialSkipped;
        public bool HasReceivedTutorialExperience;
        public int LastAcknowledgedPlayerLevel = 1;
        public List<LevelUpReward> PendingLevelUpRewards = new();
        public bool IsShieldTutorialStarted;
        public bool HasUsedTutorialShield;
        public bool EnableGameplayNotifications = true;
        public string FirstSessionReturnLevel;
        public string FirstSessionReturnScreen;
        public string FirstSessionReturnLocation;
        public string FirstSessionReturnPart;
        public bool FirstSessionReturnFromLevelUp;
        public bool FirstSessionReturnToShield;
        public bool IsAccountPromptPending;
        public bool IsAccountPromptShown;

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

        internal bool HasSerializedProgressCollection => _serializedProgress != null;
        internal IReadOnlyList<SerializableLevelProgressEntry> SerializedProgress => _serializedProgress;

        internal void EnsureSerializedProgressCollection()
        {
            _serializedProgress ??= new List<SerializableLevelProgressEntry>();
        }

        internal void ReplaceSerializedProgress(List<SerializableLevelProgressEntry> entries)
        {
            _serializedProgress = entries ?? new List<SerializableLevelProgressEntry>();
            RestoreSnapshot();
        }

        /// <summary>Привязывает сохранённые результаты к текущим адресам каталога.</summary>
        internal void RemapProgressToCatalog(HierarchicalLevelCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            EnsureSerializedProgressCollection();

            var byLegacyKey = catalog.EnumerateLevels()
                .ToDictionary(level => new LevelProgressKey(level.LocationId, level.PartId, level.LevelIndex));
            var remapped = new Dictionary<LevelProgressKey, SerializableLevelProgressEntry>();
            foreach (var serialized in _serializedProgress)
            {
                if (serialized == null) continue;
                HierarchicalLevelCatalog.LevelDescriptor descriptor;
                if (!string.IsNullOrWhiteSpace(serialized.Address))
                {
                    if (!catalog.TryFindLevelByAddress(serialized.Address, out descriptor)) continue;
                }
                else
                {
                    var legacyKey = new LevelProgressKey(
                        serialized.LocationId?.Trim() ?? string.Empty,
                        serialized.PartOfDayId?.Trim() ?? string.Empty,
                        Math.Max(0, serialized.LevelIndex));
                    if (!byLegacyKey.TryGetValue(legacyKey, out descriptor)) continue;
                }

                var key = new LevelProgressKey(descriptor.LocationId, descriptor.PartId, descriptor.LevelIndex);
                if (remapped.ContainsKey(key)) continue;
                remapped[key] = new SerializableLevelProgressEntry
                {
                    LocationId = descriptor.LocationId,
                    PartOfDayId = descriptor.PartId,
                    Address = descriptor.Address,
                    LevelIndex = descriptor.LevelIndex,
                    IsUnlocked = serialized.IsUnlocked,
                    Stars = serialized.Stars
                };
            }

            ReplaceSerializedProgress(remapped.Values.ToList());
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
                    Address = entry.Address,
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

            var models = new Dictionary<LevelProgressKey, LevelProgressEntry>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.LocationId) || string.IsNullOrWhiteSpace(entry.PartOfDayId))
                {
                    continue;
                }

                var key = new LevelProgressKey(entry.LocationId.Trim(), entry.PartOfDayId.Trim(), Math.Max(0, entry.LevelIndex));
                var value = new LevelProgressEntry(key, entry.IsUnlocked, entry.Stars, entry.Address);
                if (models.TryGetValue(key, out var existing))
                {
                    if (existing.IsUnlocked != value.IsUnlocked || existing.Stars != value.Stars)
                        throw new InvalidOperationException("Conflicting level progress entries.");
                    continue;
                }
                models.Add(key, value);
            }

            return models.Count == 0
                ? LevelProgressSnapshot.Empty
                : new LevelProgressSnapshot(models.Values);
        }

    }
}
