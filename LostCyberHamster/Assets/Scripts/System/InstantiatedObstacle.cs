using Assets.Scripts.GameEngine;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.System
{
    public class InstantiatedObstacle
    {
        public Obstacle ObstacleScript { get; }
        public string SpriteName { get; }
        public Vector3 SpawnPosition { get; }
        public int PatternIndex { get; }

        public InstantiatedObstacle(Obstacle obstacleScript, string spriteName, Vector3 spawnPosition, int patternIndex)
        {
            ObstacleScript = obstacleScript;
            SpriteName = spriteName;
            SpawnPosition = spawnPosition;
            PatternIndex = patternIndex;
        }
        public override string ToString()
        {
            return $" SpriteName: {SpriteName}, SpawnPosition: {SpawnPosition}, PatternIndex: {PatternIndex}";
        }
    }
}
