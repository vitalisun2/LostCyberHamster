using System;

namespace GameManagement.Progress
{
    /// <summary>Сохраняет купленный уровень открытой способности.</summary>
    [Serializable]
    public sealed class SuperAttackLevelProgress
    {
        public int SuperAttackId;
        public int Level = 1;
    }
}
