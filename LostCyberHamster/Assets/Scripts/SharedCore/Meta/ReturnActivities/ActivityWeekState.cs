using System;
using System.Collections.Generic;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Сохраняет цель календарной недели и точные попытки текущего периода.</summary>
    [Serializable]
    public sealed class ActivityWeekState
    {
        public string Id;
        public int ConfigVersion;
        public int TargetWins;
        public int TargetDays;
        public int Coins;
        public bool Completed;
        public List<string> AttemptIds = new();
        public List<string> Days = new();
    }
}
