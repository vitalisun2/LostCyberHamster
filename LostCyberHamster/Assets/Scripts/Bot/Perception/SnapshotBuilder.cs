using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Perception
{
    /// <summary>
    /// Собирает текущий snapshot мира для бота из runtime-состояния.
    /// </summary>
    public sealed class SnapshotBuilder
    {
        private static readonly Comparison<ObstacleSnapshot> _compareObstaclesByLeftX =
            (left, right) => left.LeftX.CompareTo(right.LeftX);

        private readonly Dictionary<int, BoxCollider2D> _obstacleCollidersByInstanceId = new();

        private Camera _camera;
        private Hamster _cachedHamster;
        private BoxCollider2D _hamsterCollider;
        private ObstacleSpawner _cachedSpawner;

        /// <summary>
        /// Строит snapshot мира по текущему состоянию хомяка и окружения.
        /// </summary>
        public WorldSnapshot Build(Hamster hamster)
        {
            // Для runtime-планирования обязательно должны быть готовы scene-зависимости.
            if (hamster == null)
                throw new InvalidOperationException(
                    "SnapshotBuilder.Build failed: hamster is null. RuntimeBotController should resolve scene dependencies before ticking.");

            Camera camera = GetCamera();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "SnapshotBuilder.Build failed: Camera.main is null. Bot runtime snapshot requires a main camera in the active scene.");
            }

            float halfWidth = camera.orthographicSize * camera.aspect;
            float screenLeftEdgeX = camera.transform.position.x - halfWidth;
            float screenRightEdgeX = camera.transform.position.x + halfWidth;

            List<ObstacleSnapshot> obstacles = CollectObstacles();
            HamsterSnapshot hamsterSnapshot = BuildHamsterSnapshot(hamster);

            return new WorldSnapshot(
                hamsterSnapshot,
                obstacles,
                screenLeftEdgeX,
                screenRightEdgeX,
                Time.time);
        }

        /// <summary>
        /// Собирает snapshot хомяка.
        /// </summary>
        private HamsterSnapshot BuildHamsterSnapshot(Hamster hamster)
        {
            // Получает runtime bounds collider хомяка.
            BoxCollider2D collider = GetHamsterCollider(hamster);
            if (collider == null)
                throw new MissingComponentException("SnapshotBuilder.BuildHamsterSnapshot failed: BoxCollider2D is missing on Hamster object.");

            Bounds bounds = collider.bounds;

            // Собирает состояние хомяка.
            HamsterStateEnum hamsterState = hamster.HamsterState.Value;
            bool isOnRoof = IsRoofState(hamsterState);

            return new HamsterSnapshot(
                hamsterState,
                hamster.IsOnBottomLine.Value,
                isOnRoof,
                hamster.Energy.Value,
                hamster.Lives.Value,
                hamster.IsShifting.Value,
                isOnRoof && hamster.LastObstacle.Value != null
                    ? hamster.LastObstacle.Value.GetInstanceID()
                    : null,
                hamster.LeftX,
                hamster.RightX,
                bounds.min.y,
                bounds.max.y);
        }

        /// <summary>
        /// Собирает все active spawned obstacles.
        /// </summary>
        private List<ObstacleSnapshot> CollectObstacles()
        {
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return new List<ObstacleSnapshot>(0);

            if (_cachedSpawner != spawner)
            {
                _cachedSpawner = spawner;
                _obstacleCollidersByInstanceId.Clear();
            }

            var obstacles = new List<ObstacleSnapshot>(spawner.SpawnedObstacles.Count);

            for (int i = 0; i < spawner.SpawnedObstacles.Count; i++)
            {
                InstantiatedObstacle instantiatedObstacle = spawner.SpawnedObstacles[i];
                Obstacle obstacle = instantiatedObstacle?.ObstacleScript;
                if (obstacle == null)
                    continue;

                BoxCollider2D collider = GetObstacleCollider(obstacle);
                if (collider == null)
                    continue;

                Bounds bounds = collider.bounds;
                obstacles.Add(new ObstacleSnapshot(
                    obstacle.GetInstanceID(),
                    obstacle.ObstacleType.ObstacleTypeEnum,
                    obstacle.ObstacleType.IsTop,
                    bounds.min.x,
                    bounds.max.x,
                    bounds.center.x,
                    bounds.min.y,
                    bounds.max.y));
            }

            obstacles.Sort(_compareObstaclesByLeftX);
            return obstacles;
        }

        private Camera GetCamera()
        {
            if (_camera == null)
                _camera = Camera.main;

            return _camera;
        }

        private BoxCollider2D GetHamsterCollider(Hamster hamster)
        {
            if (_cachedHamster != hamster || _hamsterCollider == null)
            {
                _cachedHamster = hamster;
                _hamsterCollider = hamster.GetComponentInChildren<BoxCollider2D>();
            }

            return _hamsterCollider;
        }

        private BoxCollider2D GetObstacleCollider(Obstacle obstacle)
        {
            int instanceId = obstacle.GetInstanceID();
            if (_obstacleCollidersByInstanceId.TryGetValue(instanceId, out BoxCollider2D collider)
                && collider != null)
            {
                return collider;
            }

            collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider != null)
                _obstacleCollidersByInstanceId[instanceId] = collider;

            return collider;
        }

        /// <summary>
        /// Проверяет roof-состояние.
        /// </summary>
        private static bool IsRoofState(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.RoofRun
                || hamsterState == HamsterStateEnum.JumpOnRoof
                || hamsterState == HamsterStateEnum.JumpOnRoofDamage
                || hamsterState == HamsterStateEnum.RoofJump
                || hamsterState == HamsterStateEnum.RoofJumpDamage
                || hamsterState == HamsterStateEnum.SuperRoofJump
                || hamsterState == HamsterStateEnum.SuperRoofJumpDamage;
        }
    }
}
