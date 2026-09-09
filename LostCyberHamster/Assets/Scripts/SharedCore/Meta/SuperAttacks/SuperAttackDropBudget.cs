using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>Один roll на фактическое разрушение экземпляра; общий cap всех типов дропа на активацию.</summary>
    public sealed class SuperAttackDropBudget
    {
        private readonly HashSet<(int, int)> _destroyed = new();
        private readonly float _chance;
        private readonly int _maximumDrops;
        public int DestroyedCount => _destroyed.Count;
        public int DropsCreated { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static float? TestingRoll { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTestingRoll() => TestingRoll = null;
#endif

        public SuperAttackDropBudget(SuperAttackLevelData level)
        {
            _chance = level.DropChance;
            _maximumDrops = level.MaximumDrops;
        }

        /// <summary>Вызывается владельцем удаления до возврата препятствия в пул.</summary>
        public bool TryCreateDrop(Obstacle obstacle)
        {
            if (obstacle == null || !_destroyed.Add((obstacle.GetInstanceID(), obstacle.SpawnGeneration))) return false;
            if (_chance <= 0 || DropsCreated >= _maximumDrops) return false;
            float roll;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            roll = GameManagement.GameDataManager.IsProgressionTestingProfile && TestingRoll.HasValue
                ? TestingRoll.Value : Random.value;
#else
            roll = Random.value;
#endif
            if (roll >= _chance) return false;
            DropsCreated++;
            return true;
        }
    }
}
