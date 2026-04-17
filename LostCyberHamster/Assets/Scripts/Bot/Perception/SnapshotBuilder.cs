using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Perception
{
    public sealed class SnapshotBuilder
    {
        private const float ExtraVisionScreenFraction = 0.5f;

        public WorldSnapshot Build(Hamster hamster)
        {
            Camera camera = Camera.main;
            if (hamster == null || camera == null)
                return null;

            float halfWidth = camera.orthographicSize * camera.aspect;
            float screenLeftEdgeX = camera.transform.position.x - halfWidth;
            float screenRightEdgeX = camera.transform.position.x + halfWidth;
            float visionRightEdgeX = screenRightEdgeX + halfWidth;

            List<ObstacleSnapshot> obstacles = CollectObstacles(screenLeftEdgeX, visionRightEdgeX);
            HamsterSnapshot hamsterSnapshot = BuildHamsterSnapshot(hamster);

            return new WorldSnapshot(
                hamsterSnapshot,
                obstacles,
                screenLeftEdgeX,
                screenRightEdgeX,
                visionRightEdgeX,
                Time.time);
        }

        private static HamsterSnapshot BuildHamsterSnapshot(Hamster hamster)
        {
            return new HamsterSnapshot(
                hamster.HamsterState.Value,
                hamster.IsOnBottomLine.Value,
                IsRoofState(hamster.HamsterState.Value),
                hamster.Energy.Value,
                hamster.Lives.Value,
                hamster.IsDamaged.Value,
                hamster.IsShifting.Value,
                hamster.LastObstacle.Value != null ? hamster.LastObstacle.Value.GetInstanceID() : null,
                hamster.LeftX,
                hamster.RightX);
        }

        private static List<ObstacleSnapshot> CollectObstacles(float screenLeftEdgeX, float visionRightEdgeX)
        {
            var obstacles = new List<ObstacleSnapshot>();
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return obstacles;

            for (int i = 0; i < spawner.SpawnedObstacles.Count; i++)
            {
                InstantiatedObstacle instantiatedObstacle = spawner.SpawnedObstacles[i];
                Obstacle obstacle = instantiatedObstacle?.ObstacleScript;
                if (obstacle == null)
                    continue;

                BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
                if (collider == null)
                    continue;

                Bounds bounds = collider.bounds;
                if (bounds.max.x < screenLeftEdgeX || bounds.min.x > visionRightEdgeX)
                    continue;

                obstacles.Add(new ObstacleSnapshot(
                    obstacle.GetInstanceID(),
                    obstacle.ObstacleType.ObstacleTypeEnum,
                    obstacle.ObstacleType.IsTop,
                    bounds.min.x,
                    bounds.max.x,
                    bounds.center.x));
            }

            obstacles.Sort((left, right) => left.LeftX.CompareTo(right.LeftX));
            return obstacles;
        }

        private static bool IsRoofState(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.RoofRun
                || hamsterState == HamsterStateEnum.RoofJump
                || hamsterState == HamsterStateEnum.RoofJumpDamage
                || hamsterState == HamsterStateEnum.SuperRoofJump
                || hamsterState == HamsterStateEnum.SuperRoofJumpDamage;
        }
    }
}
