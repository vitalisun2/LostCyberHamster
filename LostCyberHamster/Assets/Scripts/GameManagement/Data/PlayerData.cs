using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vues.GameCore;

namespace GameManagement
{
    [Serializable]
    public class PlayerData
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
        public string CurrentLevel = "level_01";

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
        public Dictionary<string, int> OpenedLevels => LevelStars.Select((stars, index) => new { LevelName = $"level_{(index + 1):D2}", Stars = stars })
                                                                 .ToDictionary(x => x.LevelName, x => x.Stars);


        public List<int> LevelStars = new List<int>(){
            0
        };

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
    }
}
