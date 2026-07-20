using System;

namespace GameManagement
{
    [Serializable]
    internal sealed class SerializableLevelProgressEntry
    {
        public string LocationId;
        public string PartOfDayId;
        public int LevelIndex;
        public bool IsUnlocked;
        public int Stars;
    }
}
