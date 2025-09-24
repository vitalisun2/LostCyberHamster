using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Assets.Scripts.Common.Models;
using Vues.GameCore;

namespace GameManagement
{
    [Serializable]
    public class PlayerData : ISerializationCallbackReceiver
    {
        /// <summary>
        /// Количество денег
        /// </summary>
        public int Money;

        /// <summary>
        /// Количество кристаллов
        /// </summary>
        public int Crystals;

        /// <summary>
        /// Идентификатор примененного скина
        /// </summary>
        public int AppliedSkinId = 0;

        /// <summary>
        /// Список купленных скинов
        /// </summary>
        public List<int> PurchasedSkinIds = new() { 0 };

        /// <summary>
        /// Текущий уровень
        /// </summary>
        public LevelKey CurrentLevelKey = new("new_york", PartOfDay.Morning, 1);

        [SerializeField]
        [FormerlySerializedAs("CurrentLevel")]
        [Obsolete("Use CurrentLevelKey instead", false)]
        private string _serializedCurrentLevel = "level_01";

        [Obsolete("Use CurrentLevelKey instead", false)]
        public string CurrentLevel
        {
            get
            {
                var compact = CurrentLevelKey.ToCompactString();
                _serializedCurrentLevel = compact;
                return compact;
            }
            set
            {
                _serializedCurrentLevel = value;
                if (LevelKey.TryParse(value, out var key))
                {
                    CurrentLevelKey = key;
                }
            }
        }

        /// <summary>
        /// Дата последнего обновления ежедневных заданий
        /// </summary>
        public string DailyTasksRefreshDate;

        /// <summary>
        /// Ежедневные задания
        /// </summary>
        public List<Quest> DailyTasks;

        /// <summary>
        /// Количество звезд на уровнях (пройденные уровни и открытые уровни)
        /// </summary>
        public Dictionary<LevelKey, int> StarsByLevel = new();

        [Obsolete("Use StarsByLevel instead", false)]
        public List<int> LevelStars = new()
        {
            0
        };

        [Obsolete("Use StarsByLevel instead", false)]
        public Dictionary<string, int> OpenedLevels = new();

        public int DataVersion = 2;

        /// <summary>
        /// Выполненые сюжетные квесты
        /// </summary>
        public Dictionary<string, bool> ComplitedStorylineQuests = new Dictionary<string, bool>();

        /// <summary>
        /// Дата последнего сохранения
        /// </summary>
        public string LastSaveDate = DateTime.MinValue.ToString("o");

        /// <summary>
        /// Первый запуск игры
        /// </summary>
        public bool IsFirstLaunch = true;


        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static PlayerData FromJson(string json)
        {
            return JsonUtility.FromJson<PlayerData>(json);
        }

        public void OnBeforeSerialize()
        {
            CurrentLevel = CurrentLevelKey.ToCompactString();

            if (OpenedLevels == null)
            {
                OpenedLevels = new Dictionary<string, int>();
            }
            else
            {
                OpenedLevels.Clear();
            }

            if (StarsByLevel != null)
            {
                foreach (var kvp in StarsByLevel)
                {
                    OpenedLevels[kvp.Key.ToCompactString()] = kvp.Value;
                }
            }
        }

        public void OnAfterDeserialize()
        {
            StarsByLevel ??= new Dictionary<LevelKey, int>();

            var serializedLevel = _serializedCurrentLevel;

            if (DataVersion < 2)
            {
                if (!string.IsNullOrEmpty(serializedLevel) && LevelKey.TryParse(serializedLevel, out var parsedLevel))
                {
                    CurrentLevelKey = parsedLevel;
                }
                else if (!string.IsNullOrEmpty(serializedLevel))
                {
                    CurrentLevelKey = LegacyStringToKey(serializedLevel);
                }

                StarsByLevel.Clear();

                if (LevelStars != null)
                {
                    for (int i = 0; i < LevelStars.Count; i++)
                    {
                        if (LevelStars[i] > 0)
                        {
                            var key = LegacyIndexToKey(i + 1);
                            StarsByLevel[key] = LevelStars[i];
                        }
                    }
                }

                if (OpenedLevels != null)
                {
                    foreach (var kv in OpenedLevels)
                    {
                        StarsByLevel[LegacyStringToKey(kv.Key)] = kv.Value;
                    }
                }

                DataVersion = 2;
            }
            else
            {
                StarsByLevel.Clear();

                if (OpenedLevels != null)
                {
                    foreach (var kv in OpenedLevels)
                    {
                        if (LevelKey.TryParse(kv.Key, out var key))
                        {
                            StarsByLevel[key] = kv.Value;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(serializedLevel))
                {
                    if (LevelKey.TryParse(serializedLevel, out var currentKey))
                    {
                        CurrentLevelKey = currentKey;
                    }
                    else
                    {
                        CurrentLevelKey = LegacyStringToKey(serializedLevel);
                    }
                }
            }

            _serializedCurrentLevel = CurrentLevelKey.ToCompactString();
        }

        private static readonly string[] LocationIds = { "new_york", "paris", "tokyo", "moscow" };

        private static LevelKey LegacyIndexToKey(int idx)
        {
            int locIdx = (idx - 1) / 4;
            int partIdx = (idx - 1) % 4;
            return new LevelKey(LocationIds[locIdx], (PartOfDay)(partIdx + 1), 1);
        }

        private static LevelKey LegacyStringToKey(string s)
        {
            int n = int.Parse(s.Substring("level_".Length));
            return LegacyIndexToKey(n);
        }
    }
}
