using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Хранит ближайшую цепочку препятствий и их индексы в world snapshot.
    /// </summary>
    public sealed class ObstacleChain
    {
        public ObstacleChain(
            IReadOnlyList<ObstacleSnapshot> obstacles,
            IReadOnlyList<int> indices)
        {
            if (obstacles == null)
                throw new ArgumentNullException(nameof(obstacles));

            if (indices == null)
                throw new ArgumentNullException(nameof(indices));

            if (obstacles.Count == 0 || obstacles.Count != indices.Count)
                throw new ArgumentException("Obstacle chain must contain matching obstacle/index pairs.");

            Obstacles = new List<ObstacleSnapshot>(obstacles);
            Indices = new List<int>(indices);
        }

        public IReadOnlyList<ObstacleSnapshot> Obstacles { get; }
        public IReadOnlyList<int> Indices { get; }
        public int Count => Obstacles.Count;
        public ObstacleSnapshot FirstObstacle => Obstacles[0];
        public int FirstIndex => Indices[0];

        /// <summary>
        /// Возвращает obstacle и world index по индексу внутри chain.
        /// </summary>
        public bool TryGetAt(
            int chainIndex,
            out ObstacleSnapshot obstacle,
            out int worldIndex)
        {
            if (chainIndex < 0 || chainIndex >= Count)
            {
                obstacle = null;
                worldIndex = -1;
                return false;
            }

            obstacle = Obstacles[chainIndex];
            worldIndex = Indices[chainIndex];
            return true;
        }

        /// <summary>
        /// Находит первую крышу внутри chain.
        /// </summary>
        public bool TryFindFirstRoof(
            out ObstacleSnapshot roofObstacle,
            out int roofWorldIndex,
            out int roofChainIndex)
        {
            for (int chainIndex = 0; chainIndex < Count; chainIndex++)
            {
                ObstacleSnapshot obstacle = Obstacles[chainIndex];
                if (!ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                    continue;

                roofObstacle = obstacle;
                roofWorldIndex = Indices[chainIndex];
                roofChainIndex = chainIndex;
                return true;
            }

            roofObstacle = null;
            roofWorldIndex = -1;
            roofChainIndex = -1;
            return false;
        }

        /// <summary>
        /// Возвращает true, если на крыше obstacle из chain есть опасный occupant.
        /// </summary>
        public bool HasDamagingRoofOccupant(int roofChainIndex)
        {
            if (roofChainIndex < 0 || roofChainIndex >= Count)
                return false;

            var obstacleData = new JumpObstacleData[Count];
            for (int chainIndex = 0; chainIndex < Count; chainIndex++)
            {
                ObstacleSnapshot obstacle = Obstacles[chainIndex];
                obstacleData[chainIndex] = new JumpObstacleData(
                    obstacle.ObstacleType,
                    obstacle.IsBottomLine,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX);
            }

            return JumpOutcomeResolver.TryFindDamagingRoofOccupantOnRoof(
                obstacleData,
                roofChainIndex,
                out _);
        }

        /// <summary>
        /// Проверяет, входит ли obstacle с указанным instance id в chain.
        /// </summary>
        public bool ContainsObstacle(ObstacleSnapshot targetObstacle)
        {
            if (targetObstacle == null)
                return false;

            for (int chainIndex = 0; chainIndex < Count; chainIndex++)
            {
                if (Obstacles[chainIndex].InstanceId == targetObstacle.InstanceId)
                    return true;
            }

            return false;
        }
    }
}