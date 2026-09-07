using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameManagement.Leaderboard
{
    /// <summary>Хранит последние таблицы отдельно от игрового сохранения и очереди результатов.</summary>
    [Serializable]
    internal sealed class LeaderboardReadCache
    {
        public List<LeaderboardResultsSnapshot> Tables = new();

        /// <summary>Повреждение необязательного кеша оставляет игровое сохранение нетронутым.</summary>
        public static LeaderboardReadCache Load(string environment)
        {
            try
            {
                var json = PlayerPrefs.GetString(Key(environment), string.Empty);
                var cache = string.IsNullOrEmpty(json) ? new LeaderboardReadCache() :
                    JsonUtility.FromJson<LeaderboardReadCache>(json);
                cache ??= new LeaderboardReadCache();
                cache.Tables ??= new List<LeaderboardResultsSnapshot>();
                cache.Tables.RemoveAll(table => table == null || table.Environment != environment ||
                    !LeaderboardService.ConfiguredLeaderboardIds.Contains(table.LeaderboardId) ||
                    string.IsNullOrWhiteSpace(table.VersionId) || table.Entries == null ||
                    table.Entries.Count > 50 || table.Entries.Any(entry => entry == null));
                cache.Tables = cache.Tables.GroupBy(table => table.LeaderboardId)
                    .Select(group => group.Last()).ToList();
                foreach (var table in cache.Tables)
                    if (table.Player != null && string.IsNullOrWhiteSpace(table.Player.PlayerId)) table.Player = null;
                return cache;
            }
            catch (Exception)
            {
                return new LeaderboardReadCache();
            }
        }

        /// <summary>Отказ записи кеша не отменяет уже полученный ответ.</summary>
        public bool TrySave(string environment)
        {
            try
            {
                PlayerPrefs.SetString(Key(environment), JsonUtility.ToJson(this));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception) { return false; }
        }

        private static string Key(string environment) => "Leaderboard.ReadCache.1." + environment;
    }
}
