using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Strategies.Shared.Execution
{
    /// <summary>
    /// Ищет runtime obstacle по сохранённому instance id.
    /// </summary>
    internal sealed class LiveObstacleResolver
    {
        public Obstacle Find(int instanceId)
        {
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

            for (int obstacleIndex = 0; obstacleIndex < spawner.SpawnedObstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = spawner.SpawnedObstacles[obstacleIndex]?.ObstacleScript;
                if (obstacle != null && obstacle.GetInstanceID() == instanceId)
                    return obstacle;
            }

            return null;
        }
    }
}
