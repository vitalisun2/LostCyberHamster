using System;

namespace Vues.GameCore
{
    /// <summary>Каталожные параметры одного уровня способности.</summary>
    [Serializable]
    public sealed class SuperAttackLevelData
    {
        public int Level;
        public float Duration;
        public float RangeMultiplier = 1f;
        public bool DestroysOnCollision;
        public float DropChance;
        public int MaximumDrops;
        public int JumpCombinations;
    }
}
