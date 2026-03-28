using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Поиск живых (spawned) препятствий по StableId и вычисление дистанции.
    /// </summary>
    internal static class LiveObstacleFinder
    {
        public static Obstacle Find(int stableId)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                if (inst.ObstacleScript.GetInstanceID() == stableId)
                    return inst.ObstacleScript;
            }

            return null;
        }

        public static float GetDistance(ObstacleInfo target, float hamsterRightX)
        {
            var live = Find(target.StableId);
            if (live == null)
                return target.DistanceToHamster;

            float leftX = live.transform.position.x - live.ColliderWidth * 0.5f;
            return leftX - hamsterRightX;
        }
    }
}
