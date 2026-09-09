using System;
using UnityEngine;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Читает и проверяет небольшой локальный каталог активностей.</summary>
    [Serializable]
    public sealed class ReturnActivityConfig
    {
        public int Version;
        public bool Enabled;
        public ActivityCurrencyReward[] Days;
        public int WeeklyWins;
        public int WeeklyDays;
        public int WeeklyCoins;
        private static ReturnActivityConfig _current;
        private static bool _loaded;

        public static ReturnActivityConfig Current
        {
            get
            {
                if (_loaded) return _current;
                _loaded = true;
                var asset = Resources.Load<TextAsset>("ReturnActivities/return_activities");
                if (asset == null) return null;
                try
                {
                    var config = JsonUtility.FromJson<ReturnActivityConfig>(asset.text);
                    if (config != null && config.IsValid()) _current = config;
                }
                catch (Exception exception) { Debug.LogError($"[Activities] Некорректный каталог: {exception.Message}"); }
                return _current;
            }
        }

        public bool IsValid()
        {
            if (Version < 1 || Days == null || Days.Length != 7 || WeeklyWins < 1 ||
                WeeklyDays < 1 || WeeklyDays > 7 || WeeklyWins < WeeklyDays || WeeklyCoins <= 0) return false;
            foreach (var reward in Days)
                if (reward == null || reward.Coins < 0 || reward.Gems < 0 ||
                    reward.Coins == 0 && reward.Gems == 0) return false;
            return true;
        }
    }
}
